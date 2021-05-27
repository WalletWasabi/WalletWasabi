using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client
{
	public class CoinJoinClient
	{
		public CoinJoinClient(
			uint256 roundId,
			IWabiSabiApiRequestHandler arenaRequestHandler,
			IEnumerable<Coin> coins,
			Kitchen kitchen,
			KeyManager keymanager)
		{
			RoundId = roundId;
			ArenaRequestHandler = arenaRequestHandler;
			Kitchen = kitchen;
			Keymanager = keymanager;
			Coins = coins;
		}

		private uint256 RoundId { get; }
		private RoundState RoundState { get; set; }
		private ZeroCredentialPool ZeroAmountCredentialPool { get; } = new();
		private ZeroCredentialPool ZeroVsizeCredentialPool { get; } = new();
		private IEnumerable<Coin> Coins { get; set; }
		private SecureRandom SecureRandom { get; } = new SecureRandom();
		private Random Random { get; } = new();
		public IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }

		public async Task StartCoinJoinAsync(CancellationToken cancellationToken)
		{
			await RefreshRoundAsync(cancellationToken).ConfigureAwait(false);
			var constructionState = RoundState.Assert<ConstructionState>();

			// Calculate outputs values
			var outputValues = DecomposeAmounts();

			// Get all locked internal keys we have and assert we have enough.
			Keymanager.AssertLockedInternalKeysIndexed(howMany: Coins.Count());
			var allLockedInternalKeys = Keymanager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked);
			var outputs = outputValues.Zip(allLockedInternalKeys, (amount, hdPubKey) => new TxOut(amount, hdPubKey.P2wpkhScript));

			var plan = CreatePlan(
				Coins.Select(x => (ulong)x.Amount.Satoshi),
				Coins.Select(x => (ulong)x.ScriptPubKey.EstimateInputVsize()),
				outputValues);

			List<AliceClient> aliceClients = CreateAliceClients();

			// Register coins.
			aliceClients = await RegisterCoinsAsync(aliceClients, cancellationToken).ConfigureAwait(false);

			// Confirm coins.
			aliceClients = await ConfirmConnectionsAsync(aliceClients, cancellationToken).ConfigureAwait(false);

			// Output registration.
			// Here we should have something like:
			// RoundState roundState = await OutputRegistrationPhase.ConfigureAwait(false);
			await WaitFor(Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
			var outputsWithCredentials = outputs.Zip(aliceClients, (output, alice) => (output, alice.RealAmountCredentials, alice.RealVsizeCredentials));
			await RegisterOutputsAsync(outputsWithCredentials, cancellationToken).ConfigureAwait(false);

			await WaitFor(Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
			var signingState = RoundState.Assert<SigningState>();
			var unsignedCoinJoin = signingState.CreateUnsignedTransaction();

			// Sanity check.
			SanityCheck(outputs, unsignedCoinJoin, cancellationToken);

			// Send signature.
			await SignTransactionAsync(aliceClients, unsignedCoinJoin, cancellationToken).ConfigureAwait(false);
		}

		private async Task WaitFor(Phase expectedPhase, CancellationToken cancellationToken)
		{
			// ideally this should await for a CompletionTask<RoundState> instead of
			// iterate in this absurd way.
			while (RoundState.Phase < expectedPhase)
			{
				await RefreshRoundAsync(cancellationToken).ConfigureAwait(false);
				await Task.Delay(500).ConfigureAwait(false);
			}
		}

		private async Task RefreshRoundAsync(CancellationToken cancellationToken)
		{
			// this code is part of a `RoundUpdater` background service that fetches this information
			// periodically (PerioricRunner?)
			RoundState[] roundStates = await ArenaRequestHandler.GetStatusAsync(cancellationToken).ConfigureAwait(false);
			RoundState = roundStates.Single(x => x.Id == RoundId);
		}

		private List<AliceClient> CreateAliceClients()
		{
			List<AliceClient> aliceClients = new();
			foreach (var coin in Coins)
			{
				var aliceArenaClient = new ArenaClient(
					RoundState.AmountCredentialIssuerParameters,
					RoundState.VsizeCredentialIssuerParameters,
					ZeroAmountCredentialPool,
					ZeroVsizeCredentialPool,
					ArenaRequestHandler,
					SecureRandom);

				var hdKey = Keymanager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey).Single();
				var secret = hdKey.PrivateKey.GetBitcoinSecret(Keymanager.GetNetwork());
				aliceClients.Add(new AliceClient(RoundState.Id, aliceArenaClient, coin, RoundState.FeeRate, secret));
			}
			return aliceClients;
		}

		private async Task<List<AliceClient>> RegisterCoinsAsync(IEnumerable<AliceClient> aliceClients, CancellationToken cancellationToken)
		{
			async Task<AliceClient?> RegisterInputTask(AliceClient aliceClient)
			{
				try
				{
					await aliceClient.RegisterInputAsync(cancellationToken).ConfigureAwait(false);
					return aliceClient;
				}
				catch (Exception e)
				{
					Logger.LogWarning($"Round ({RoundState.Id}), Alice ({aliceClient.AliceId}): {nameof(AliceClient.RegisterInputAsync)} failed, reason:'{e}'.");
					return default;
				}
			}

			var registerRequests = aliceClients.Select(RegisterInputTask);
			var completedRequests = await Task.WhenAll(registerRequests).ConfigureAwait(false);

			return completedRequests.Where(x => x is not null).Cast<AliceClient>().ToList();
		}

		private async Task<List<AliceClient>> ConfirmConnectionsAsync(IEnumerable<AliceClient> aliceClients, CancellationToken cancellationToken)
		{
			async Task<AliceClient?> ConfirmConnectionTask(AliceClient aliceClient)
			{
				try
				{
					await aliceClient.ConfirmConnectionAsync(cancellationToken).ConfigureAwait(false);
					return aliceClient;
				}
				catch (Exception e)
				{
					Logger.LogWarning($"Round ({RoundState.Id}), Alice ({aliceClient.AliceId}): {nameof(AliceClient.ConfirmConnectionAsync)} failed, reason:'{e}'.");
					return default;
				}
			}

			var confirmationRequests = aliceClients.Select(ConfirmConnectionTask);
			var completedRequests = await Task.WhenAll(confirmationRequests).ConfigureAwait(false);

			return completedRequests.Where(x => x is not null).Cast<AliceClient>().ToList();
		}

		private IEnumerable<Money> DecomposeAmounts()
		{
			return Coins.Select(c => c.Amount - RoundState.FeeRate.GetFee(c.ScriptPubKey.EstimateInputVsize()));
		}

		private IEnumerable<IEnumerable<(ulong RealAmountCredentialValue, ulong RealVsizeCredentialValue, Money Value)>> CreatePlan(
			IEnumerable<ulong> realAmountCredentialValues,
			IEnumerable<ulong> realVsizeCredentialValues,
			IEnumerable<Money> outputValues)
		{
			yield return realAmountCredentialValues.Zip(realVsizeCredentialValues, outputValues, (a, v, o) => (a, v, o));
		}

		private async Task RegisterOutputsAsync(
			IEnumerable<(TxOut Output, Credential[] RealAmountCredentials, Credential[] RealVsizeCredentials)> outputsWithCredentials,
			CancellationToken cancellationToken)
		{
			async Task<TxOut?> RegisterOutputTask(BobClient bobClient, TxOut output, Credential[] realAmountCredentials, Credential[] realVsizeCredentials)
			{
				try
				{
					await bobClient.RegisterOutputAsync(output.Value, output.ScriptPubKey, realAmountCredentials, realVsizeCredentials, cancellationToken).ConfigureAwait(false);
					return output;
				}
				catch (Exception e)
				{
					Logger.LogWarning($"Round ({RoundState.Id}), Bob ({{output.ScriptPubKey}}): {nameof(BobClient.RegisterOutputAsync)} failed, reason:'{e}'.");
					return default;
				}
			}

			var bobClients = Enumerable.Range(0, int.MaxValue).Select(_ => CreateBobClient());
			var outputRegisterRequests = bobClients.Zip(
					outputsWithCredentials,
					(bobClient, data) => RegisterOutputTask(bobClient, data.Output, data.RealAmountCredentials, data.RealVsizeCredentials));

			await Task.WhenAll(outputRegisterRequests).ConfigureAwait(false);
		}

		private BobClient CreateBobClient()
		{
			return new BobClient(
				RoundState.Id,
				new(
					RoundState.AmountCredentialIssuerParameters,
					RoundState.VsizeCredentialIssuerParameters,
					ZeroAmountCredentialPool,
					ZeroVsizeCredentialPool,
					ArenaRequestHandler,
					SecureRandom));
		}

		private void SanityCheck(IEnumerable<TxOut> outputs, Transaction unsignedCoinJoinTransaction, CancellationToken cancellationToken)
		{
			var coinJoinOutputs = unsignedCoinJoinTransaction.Outputs.Select(o => (o.Value, o.ScriptPubKey));
			var expectedOutputs = outputs.Select(o => (o.Value, o.ScriptPubKey));
			if (coinJoinOutputs.IsSuperSetOf(expectedOutputs))
			{
				throw new InvalidOperationException($"Round ({RoundState.Id}): My output is missing.");
			}
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
					Logger.LogWarning($"Round ({RoundState.Id}), Alice ({{aliceClient.AliceId}}): {nameof(AliceClient.SignTransactionAsync)} failed, reason:'{e}'.");
					return default;
				}
			}

			var signingRequests = aliceClients.Select(SignTransactionTask);
			await Task.WhenAll(signingRequests).ConfigureAwait(false);
		}
	}
}
