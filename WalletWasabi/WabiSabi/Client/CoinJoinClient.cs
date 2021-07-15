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
using WalletWasabi.WabiSabi.Crypto;
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
			IEnumerable<Coin> coins,
			Kitchen kitchen,
			KeyManager keymanager,
			RoundStateUpdater roundStatusUpdater)
		{
			ArenaRequestHandler = arenaRequestHandler;
			Kitchen = kitchen;
			Keymanager = keymanager;
			RoundStatusUpdater = roundStatusUpdater;
			SecureRandom = new SecureRandom();
			Coins = coins;
		}

		private IEnumerable<Coin> Coins { get; set; }
		private SecureRandom SecureRandom { get; } = new SecureRandom();
		private Random Random { get; } = new();
		public IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }
		private RoundStateUpdater RoundStatusUpdater { get; }
		public RoundState? RoundState { get; private set; }

		public async Task StartCoinJoinAsync(CancellationToken cancellationToken, IEnumerable<Money>? forcedOutputDenominations = null)
		{
			RoundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, cancellationToken).ConfigureAwait(false);
			var constructionState = RoundState.Assert<ConstructionState>();

			// Calculate outputs values
			var outputValues = DecomposeAmounts(RoundState.FeeRate, RoundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min, forcedOutputDenominations);

			// Get all locked internal keys we have and assert we have enough.
			Keymanager.AssertLockedInternalKeysIndexed(howMany: outputValues.Count());
			var allLockedInternalKeys = Keymanager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked);
			var outputTxOuts = outputValues.Zip(allLockedInternalKeys, (amount, hdPubKey) => new TxOut(amount, hdPubKey.P2wpkhScript));

			List<AliceClient> aliceClients = CreateAliceClients(RoundState);
			DependencyGraph dependencyGraph = DependencyGraph.ResolveCredentialDependencies(aliceClients.Select(a => a.Coin), outputTxOuts, RoundState.FeeRate, RoundState.MaxVsizeAllocationPerAlice);
			DependencyGraphResolver dgr = new(dependencyGraph);

			// Register coins.
			await RegisterCoinsAsync(aliceClients, cancellationToken).ConfigureAwait(false);

			RoundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.ConnectionConfirmation, cancellationToken).ConfigureAwait(false);
			// Confirm coins.
			await dgr.StartConfirmConnectionsAsync(aliceClients, dependencyGraph, RoundState.ConnectionConfirmationTimeout, cancellationToken).ConfigureAwait(false);

			// Re-issuances.
			var bobClient = CreateBobClient(RoundState);
			await dgr.StartReissuancesAsync(aliceClients, bobClient, cancellationToken).ConfigureAwait(false);

			// Output registration.
			RoundState = await RoundStatusUpdater.CreateRoundAwaiter(RoundState.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
			await dgr.StartOutputRegistrationsAsync(outputTxOuts, bobClient, cancellationToken).ConfigureAwait(false);

			// ReadyToSign.
			await ReadyToSignAsync(aliceClients, cancellationToken).ConfigureAwait(false);

			// Signing.
			RoundState = await RoundStatusUpdater.CreateRoundAwaiter(RoundState.Id, rs => rs.Phase == Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
			var signingState = RoundState.Assert<SigningState>();
			var unsignedCoinJoin = signingState.CreateUnsignedTransaction();

			// Sanity check.
			if (!SanityCheck(outputTxOuts, unsignedCoinJoin))
			{
				throw new InvalidOperationException($"Round ({RoundState.Id}): My output is missing.");
			}

			// Send signature.
			await SignTransactionAsync(aliceClients, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);
		}

		private List<AliceClient> CreateAliceClients(RoundState roundState)
		{
			List<AliceClient> aliceClients = new();
			foreach (var coin in Coins)
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

		private async Task RegisterCoinsAsync(IEnumerable<AliceClient> aliceClients, CancellationToken cancellationToken)
		{
			async Task RegisterInputTask(AliceClient aliceClient)
			{
				await aliceClient.RegisterInputAsync(cancellationToken).ConfigureAwait(false);
			}

			var registerRequests = aliceClients.Select(RegisterInputTask);
			await Task.WhenAll(registerRequests).ConfigureAwait(false);
		}

		private IEnumerable<Money> DecomposeAmounts(FeeRate feeRate, Money minimumOutputAmount, IEnumerable<Money>? forcedOutputDenominations = null)
		{
			var allDenominations = forcedOutputDenominations is null ? StandardDenomination.Values : forcedOutputDenominations;
			GreedyDecomposer greedyDecomposer = new(allDenominations.Where(x => x >= minimumOutputAmount));
			var sum = Coins.Sum(c => c.EffectiveValue(feeRate));
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
