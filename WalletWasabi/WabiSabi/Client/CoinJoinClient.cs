using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;
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
		private const int MaxInputsRegistrableByWallet = 7; // how many
		private volatile bool _inCriticalCoinJoinState;

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
		public IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }
		private RoundStateUpdater RoundStatusUpdater { get; }

		public bool InCriticalCoinJoinState
		{
			get => _inCriticalCoinJoinState;
			private set => _inCriticalCoinJoinState = value;
		}

		public async Task<bool> StartCoinJoinAsync(IEnumerable<SmartCoin> coins, CancellationToken cancellationToken)
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
		public async Task<bool> StartRoundAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
		{
			var constructionState = roundState.Assert<ConstructionState>();

			var coinCandidates = SelectCoinsForRound(smartCoins, constructionState.Parameters);

			// Register coins.
			var registeredAliceClients = await CreateRegisterAndConfirmCoinsAsync(coinCandidates, roundState, cancellationToken).ConfigureAwait(false);
			if (!registeredAliceClients.Any())
			{
				throw new InvalidOperationException($"Round ({roundState.Id}): There is no available alices to participate with.");
			}

			try
			{
				InCriticalCoinJoinState = true;

				// Calculate outputs values
				var registeredCoins = registeredAliceClients.Select(x => x.SmartCoin.Coin);
				var availableVsize = registeredAliceClients.SelectMany(x => x.IssuedVsizeCredentials).Sum(x => x.Value);

				// Calculate outputs values
				roundState = await RoundStatusUpdater.CreateRoundAwaiter(rs => rs.Id == roundState.Id, cancellationToken).ConfigureAwait(false);
				constructionState = roundState.Assert<ConstructionState>();
				AmountDecomposer amountDecomposer = new(roundState.FeeRate, roundState.CoinjoinState.Parameters.AllowedOutputAmounts.Min, Constants.P2WPKHOutputSizeInBytes, (int)availableVsize);
				var theirCoins = constructionState.Inputs.Except(registeredCoins);
				var outputValues = amountDecomposer.Decompose(registeredCoins, theirCoins);

				// Get all locked internal keys we have and assert we have enough.
				Keymanager.AssertLockedInternalKeysIndexed(howMany: outputValues.Count());
				var allLockedInternalKeys = Keymanager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked);
				var outputTxOuts = outputValues.Zip(allLockedInternalKeys, (amount, hdPubKey) => new TxOut(amount, hdPubKey.P2wpkhScript));

				DependencyGraph dependencyGraph = DependencyGraph.ResolveCredentialDependencies(registeredCoins, outputTxOuts, roundState.FeeRate, roundState.MaxVsizeAllocationPerAlice);
				DependencyGraphTaskScheduler scheduler = new(dependencyGraph);

				// Re-issuances.
				var bobClient = CreateBobClient(roundState);
				await scheduler.StartReissuancesAsync(registeredAliceClients, bobClient, cancellationToken).ConfigureAwait(false);

				// Output registration.
				roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
				await scheduler.StartOutputRegistrationsAsync(outputTxOuts, bobClient, cancellationToken).ConfigureAwait(false);

				// ReadyToSign.
				await ReadyToSignAsync(registeredAliceClients, cancellationToken).ConfigureAwait(false);

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
				await SignTransactionAsync(registeredAliceClients, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);

				var finalRoundState = await RoundStatusUpdater.CreateRoundAwaiter(s => s.Id == roundState.Id && s.Phase == Phase.Ended, cancellationToken).ConfigureAwait(false);

				return finalRoundState.WasTransactionBroadcast;
			}
			finally
			{
				InCriticalCoinJoinState = false;
			}
		}

		private async Task<ImmutableArray<AliceClient>> CreateRegisterAndConfirmCoinsAsync(IEnumerable<SmartCoin> smartCoins, RoundState roundState, CancellationToken cancellationToken)
		{
			async Task<AliceClient?> RegisterInputTask(SmartCoin coin)
			{
				try
				{
					var aliceArenaClient = new ArenaClient(
						roundState.CreateAmountCredentialClient(SecureRandom),
						roundState.CreateVsizeCredentialClient(SecureRandom),
						ArenaRequestHandler);

					var hdKey = Keymanager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey).Single();
					var secret = hdKey.PrivateKey.GetBitcoinSecret(Keymanager.GetNetwork());
					if (hdKey.PrivateKey.PubKey.WitHash.ScriptPubKey != coin.ScriptPubKey)
					{
						throw new InvalidOperationException("The key cannot generate the utxo scriptpubkey. This could happen if the wallet password is not the correct one.");
					}

					return await AliceClient.CreateRegisterAndConfirmInputAsync(roundState, aliceArenaClient, coin, secret, RoundStatusUpdater, cancellationToken).ConfigureAwait(false);
				}
				catch (HttpRequestException)
				{
					return null;
				}
			}

			var aliceClients = smartCoins.Select(RegisterInputTask).ToImmutableArray();
			await Task.WhenAll(aliceClients).ConfigureAwait(false);

			return aliceClients
				.Where(x => x.Result is not null)
				.Select(x => x.Result!)
				.ToImmutableArray();
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

		// Selects coin candidates for participating in a round.
		// The criteria is the following:
		// * Only coin with amount in the allowed range
		// * Only coins with allowed script types
		// * Only one coin (the biggest one) from the same transaction (do not consolidate same transaction outputs)
		//
		// Then prefer:
		// * less private coins should be the first ones
		// * bigger coins first (this makes economical sense because mix more money paying less network fees)
		//
		// Note: this method works on already pre-filteres coins: those available and that didn't reached the
		// expected anonymity set threshold.
		private ImmutableList<SmartCoin> SelectCoinsForRound(IEnumerable<SmartCoin> coins, MultipartyTransactionParameters parameters) =>
			coins
				.Where(x => parameters.AllowedInputAmounts.Contains(x.Amount))
				.Where(x => parameters.AllowedInputTypes.Any(t => x.ScriptPubKey.IsScriptType(t)))
				.GroupBy(x => x.TransactionId)
				.Select(x => x.OrderByDescending(y => y.Amount).First())
				.OrderBy(x => x.HdPubKey.AnonymitySet)
				.ThenByDescending(x => x.Amount)
				.Take(MaxInputsRegistrableByWallet)
				.ToImmutableList();
	}
}
