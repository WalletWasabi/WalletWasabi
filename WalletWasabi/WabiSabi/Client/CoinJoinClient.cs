using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Decomposition;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinJoinClient
	{
		public CoinJoinClient(
			IWabiSabiApiRequestHandler arenaRequestHandler,
			Kitchen kitchen,
			KeyManager keymanager,
			RoundStateUpdater roundStatusUpdater)
		{
			ArenaRequestHandler = arenaRequestHandler;
			Kitchen = kitchen;
			Keymanager = keymanager;
			RoundStatusUpdater = roundStatusUpdater;
			SecureRandom = new SecureRandom();
		}

		private SecureRandom SecureRandom { get; }
		private Random Random { get; } = new();
		public IWabiSabiApiRequestHandler ArenaRequestHandler { get; private set; }
		public uint256 RoundId { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }
		private RoundStateUpdater RoundStatusUpdater { get; }

		public async Task<bool> StartCoinJoinAsync(IEnumerable<Coin> coins, CancellationToken cancellationToken)
		{
			var currentRoundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, cancellationToken).ConfigureAwait(false);

			// This should be roughly log(#inputs), it could be set slightly
			// higher if more inputs are observed but that involves trusting the
			// coordinator with those values. Therefore, conservatively set this
			// so that a maximum of 5 blame rounds are executed.
			// FIXME should smaller rounds abort earlier?
			var tryLimit = 6;

			for (var tries = 0; tries < tryLimit; tries++)
			{
				if (await StartRoundAsync(coins, currentRoundState, cancellationToken).ConfigureAwait(false))
				{
					return true;
				}
				else
				{
					var blameRoundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.BlameOf == currentRoundState.Id, cancellationToken).ConfigureAwait(false);
					currentRoundState = blameRoundState;
				}
			}

			return false;
		}

		/// <summary>Attempt to participate in a specified dround.</summary>
		/// <param name="roundState">Defines the round parameter and state information to use.</param>
		/// <returns>Whether or not the round resulted in a successful transaction.</returns>
		public async Task<bool> StartRoundAsync(IEnumerable<Coin> coins, RoundState roundState, CancellationToken cancellationToken)
		{
			_ = roundState.Assert<ConstructionState>();

			var aliceClients = CreateAliceClients(coins, roundState);

			// Register coins.
			aliceClients = await RegisterCoinsAsync(aliceClients, cancellationToken).ConfigureAwait(false);

			if (!aliceClients.Any())
			{
				throw new InvalidOperationException($"Round ({roundState.Id}): Failed to register any coins.");
			}

			// Calculate outputs values
			var outputValues = DecomposeAmounts(aliceClients.Select(a => a.Coin), roundState.FeeRate, roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min);

			// Get all locked internal keys we have and assert we have enough.
			Keymanager.AssertLockedInternalKeysIndexed(howMany: outputValues.Count());
			var allLockedInternalKeys = Keymanager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked);
			var outputTxOuts = outputValues.Zip(allLockedInternalKeys, (amount, hdPubKey) => new TxOut(amount, hdPubKey.P2wpkhScript));
			DependencyGraph dependencyGraph = DependencyGraph.ResolveCredentialDependencies(aliceClients.Select(a => a.Coin), outputTxOuts, roundState.FeeRate, roundState.MaxVsizeAllocationPerAlice);
			DependencyGraphTaskScheduler scheduler = new(dependencyGraph);

			// Confirm coins.
			await scheduler.StartConfirmConnectionsAsync(aliceClients, dependencyGraph, roundState.ConnectionConfirmationTimeout, RoundStatusUpdater, cancellationToken).ConfigureAwait(false);

			// Re-issuances.
			var bobClient = CreateBobClient(roundState);
			await scheduler.StartReissuancesAsync(aliceClients, bobClient, cancellationToken).ConfigureAwait(false);

			// Output registration.
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
			await scheduler.StartOutputRegistrationsAsync(outputTxOuts, bobClient, cancellationToken).ConfigureAwait(false);

			// ReadyToSign.
			await ReadyToSignAsync(aliceClients, cancellationToken).ConfigureAwait(false);

			// Signing.
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
			var signingState = roundState.Assert<SigningState>();
			var unsignedCoinJoin = signingState.CreateUnsignedTransaction();

			// Sanity check.
			if (!SanityCheck(outputTxOuts, unsignedCoinJoin))
			{
				throw new InvalidOperationException($"Round ({roundState.Id}): My output is missing.");
			}

			// Send signature.
			await SignTransactionAsync(aliceClients, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);

			var finalRoundState = await RoundStatusUpdater.CreateRoundAwaiter(s => s.Id == roundState.Id && s.Phase == Phase.Ended, cancellationToken).ConfigureAwait(false);

			return finalRoundState.WasTransactionBroadcast;
		}

		private IEnumerable<AliceClient> CreateAliceClients(IEnumerable<Coin> coins, RoundState roundState)
		{
			List<AliceClient> aliceClients = new();
			foreach (var coin in coins)
			{
				var aliceArenaClient = new ArenaClient(
					roundState.CreateAmountCredentialClient(SecureRandom),
					roundState.CreateVsizeCredentialClient(SecureRandom),
					ArenaRequestHandler);

				var hdKey = Keymanager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey).Single();
				var secret = hdKey.PrivateKey.GetBitcoinSecret(Keymanager.GetNetwork());
				aliceClients.Add(new AliceClient(roundState.Id, aliceArenaClient, coin, roundState.FeeRate, secret));
			}
			return aliceClients;
		}

		private async Task<IEnumerable<AliceClient>> RegisterCoinsAsync(IEnumerable<AliceClient> aliceClients, CancellationToken cancellationToken)
		{
			List<AliceClient> successfulAlices = new();
			List<AliceClient> shuffledAlices = new(aliceClients);
			shuffledAlices.Shuffle();

			foreach (var aliceClient in shuffledAlices)
			{
				try
				{
					await aliceClient.RegisterInputAsync(cancellationToken).ConfigureAwait(false);
					successfulAlices.Add(aliceClient);
				}
				catch (Exception e)
				{
					Logger.LogWarning($"Round ({aliceClient.RoundId}), Alice ({aliceClient.AliceId}): {nameof(AliceClient.RegisterInputAsync)} failed, reason:'{e}'.");
				}
			}

			return successfulAlices;
		}

		private static IEnumerable<Money> DecomposeAmounts(IEnumerable<Coin> coins, FeeRate feeRate, Money minimumOutputAmount)
		{
			GreedyDecomposer greedyDecomposer = new(StandardDenomination.Values.Where(x => x >= minimumOutputAmount));
			var sum = coins.Sum(c => c.EffectiveValue(feeRate));
			return greedyDecomposer.Decompose(sum, feeRate.GetFee(31));
		}

		private BobClient CreateBobClient(RoundState roundState)
		{
			return new BobClient(
				roundState.Id,
				new(
					roundState.CreateAmountCredentialClient(SecureRandom),
					roundState.CreateVsizeCredentialClient(SecureRandom),
					ArenaRequestHandler));
		}

		private bool SanityCheck(IEnumerable<TxOut> expectedOutputs, Transaction unsignedCoinJoinTransaction)
		{
			var coinJoinOutputs = unsignedCoinJoinTransaction.Outputs.Select(o => (o.Value, o.ScriptPubKey));
			var expectedOutputTuples = expectedOutputs.Select(o => (o.Value, o.ScriptPubKey));
			return coinJoinOutputs.IsSuperSetOf(expectedOutputTuples);
		}

		private async Task SignTransactionAsync(IEnumerable<AliceClient> aliceClients, Transaction unsignedCoinJoinTransaction, CancellationToken cancellationToken)
		{
			async Task<AliceClient?> SignTransactionTask(AliceClient aliceClient)
			{
				try
				{
					await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, cancellationToken).ConfigureAwait(false);
					return aliceClient;
				}
				catch (Exception e)
				{
					Logger.LogWarning($"Round ({aliceClient.RoundId}), Alice ({{aliceClient.AliceId}}): {nameof(AliceClient.SignTransactionAsync)} failed, reason:'{e}'.");
					return default;
				}
			}

			var signingRequests = aliceClients.Select(SignTransactionTask);
			await Task.WhenAll(signingRequests).ConfigureAwait(false);
		}

		private async Task ReadyToSignAsync(IEnumerable<AliceClient> aliceClients, CancellationToken cancellationToken)
		{
			async Task ReadyToSignTask(AliceClient aliceClient)
			{
				await aliceClient.ReadyToSignAsync(cancellationToken).ConfigureAwait(false);
			}

			var readyRequests = aliceClients.Select(ReadyToSignTask);
			await Task.WhenAll(readyRequests).ConfigureAwait(false);
		}
	}
}
