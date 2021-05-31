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

		private ZeroCredentialPool ZeroAmountCredentialPool { get; } = new();
		private ZeroCredentialPool ZeroVsizeCredentialPool { get; } = new();
		private IEnumerable<Coin> Coins { get; set; }
		private SecureRandom SecureRandom { get; } = new SecureRandom();
		private Random Random { get; } = new();
		public IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }
		private RoundStateUpdater RoundStatusUpdater { get; }

		public async Task StartCoinJoinAsync(CancellationToken cancellationToken)
		{
			var roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState => roundState.Phase == Phase.InputRegistration, cancellationToken).ConfigureAwait(false);
			var constructionState = roundState.Assert<ConstructionState>();

			// Calculate outputs values
			var outputValues = DecomposeAmounts(roundState.FeeRate);

			// Get all locked internal keys we have and assert we have enough.
			Keymanager.AssertLockedInternalKeysIndexed(howMany: Coins.Count());
			var allLockedInternalKeys = Keymanager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked);
			var outputs = outputValues.Zip(allLockedInternalKeys, (amount, hdPubKey) => new TxOut(amount, hdPubKey.P2wpkhScript));

			var plan = CreatePlan(
				Coins.Select(x => (ulong)x.Amount.Satoshi),
				Coins.Select(x => (ulong)x.ScriptPubKey.EstimateInputVsize()),
				outputValues);

			List<AliceClient> aliceClients = CreateAliceClients(roundState);

			// Register coins.
			aliceClients = await RegisterCoinsAsync(aliceClients, cancellationToken).ConfigureAwait(false);

			// Confirm coins.
			aliceClients = await ConfirmConnectionsAsync(aliceClients, constructionState.ConnectionConfirmationTimeout, cancellationToken).ConfigureAwait(false);

			// Output registration.
			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken).ConfigureAwait(false);
			var outputsWithCredentials = outputs.Zip(aliceClients, (output, alice) => (output, alice.RealAmountCredentials, alice.RealVsizeCredentials));
			var bobClients = Enumerable.Range(0, int.MaxValue).Select(_ => CreateBobClient(roundState));
			await RegisterOutputsAsync(bobClients, outputsWithCredentials, cancellationToken).ConfigureAwait(false);

			roundState = await RoundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.TransactionSigning, cancellationToken).ConfigureAwait(false);
			var signingState = roundState.Assert<SigningState>();
			var unsignedCoinJoin = signingState.CreateUnsignedTransaction();

			// Sanity check.
			var effectiveOutputs = outputs.Select(o => (o.Value - roundState.FeeRate.GetFee(o.ScriptPubKey.EstimateOutputVsize()), o.ScriptPubKey));
			if (!SanityCheck(effectiveOutputs, unsignedCoinJoin))
			{
				throw new InvalidOperationException($"Round ({roundState.Id}): My output is missing.");
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
					roundState.AmountCredentialIssuerParameters,
					roundState.VsizeCredentialIssuerParameters,
					ZeroAmountCredentialPool,
					ZeroVsizeCredentialPool,
					ArenaRequestHandler,
					SecureRandom);

				var hdKey = Keymanager.GetSecrets(Kitchen.SaltSoup(), coin.ScriptPubKey).Single();
				var secret = hdKey.PrivateKey.GetBitcoinSecret(Keymanager.GetNetwork());
				aliceClients.Add(new AliceClient(roundState.Id, aliceArenaClient, coin, roundState.FeeRate, secret));
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
					Logger.LogWarning($"Round ({aliceClient.RoundId}), Alice ({aliceClient.AliceId}): {nameof(AliceClient.RegisterInputAsync)} failed, reason:'{e}'.");
					return default;
				}
			}

			var registerRequests = aliceClients.Select(RegisterInputTask);
			var completedRequests = await Task.WhenAll(registerRequests).ConfigureAwait(false);

			return completedRequests.Where(x => x is not null).Cast<AliceClient>().ToList();
		}

		private async Task<List<AliceClient>> ConfirmConnectionsAsync(IEnumerable<AliceClient> aliceClients, TimeSpan connectionConfirmationTimeout, CancellationToken cancellationToken)
		{
			async Task<AliceClient?> ConfirmConnectionTask(AliceClient aliceClient)
			{
				try
				{
					await aliceClient.ConfirmConnectionAsync(connectionConfirmationTimeout, cancellationToken).ConfigureAwait(false);
					return aliceClient;
				}
				catch (Exception e)
				{
					Logger.LogWarning($"Round ({aliceClient.RoundId}), Alice ({aliceClient.AliceId}): {nameof(AliceClient.ConfirmConnectionAsync)} failed, reason:'{e}'.");
					return default;
				}
			}

			var confirmationRequests = aliceClients.Select(ConfirmConnectionTask);
			var completedRequests = await Task.WhenAll(confirmationRequests).ConfigureAwait(false);

			return completedRequests.Where(x => x is not null).Cast<AliceClient>().ToList();
		}

		private IEnumerable<Money> DecomposeAmounts(FeeRate feeRate)
		{
			return Coins.Select(c => c.Amount - feeRate.GetFee(c.ScriptPubKey.EstimateInputVsize()));
		}

		private IEnumerable<IEnumerable<(ulong RealAmountCredentialValue, ulong RealVsizeCredentialValue, Money Value)>> CreatePlan(
			IEnumerable<ulong> realAmountCredentialValues,
			IEnumerable<ulong> realVsizeCredentialValues,
			IEnumerable<Money> outputValues)
		{
			yield return realAmountCredentialValues.Zip(realVsizeCredentialValues, outputValues, (a, v, o) => (a, v, o));
		}

		private async Task RegisterOutputsAsync(
			IEnumerable<BobClient> bobClients,
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
					Logger.LogWarning($"Round ({bobClient.RoundId}), Bob ({{output.ScriptPubKey}}): {nameof(BobClient.RegisterOutputAsync)} failed, reason:'{e}'.");
					return default;
				}
			}

			var outputRegisterRequests = bobClients.Zip(
					outputsWithCredentials,
					(bobClient, data) => RegisterOutputTask(bobClient, data.Output, data.RealAmountCredentials, data.RealVsizeCredentials));

			await Task.WhenAll(outputRegisterRequests).ConfigureAwait(false);
		}

		private BobClient CreateBobClient(RoundState roundState)
		{
			return new BobClient(
				roundState.Id,
				new(
					roundState.AmountCredentialIssuerParameters,
					roundState.VsizeCredentialIssuerParameters,
					ZeroAmountCredentialPool,
					ZeroVsizeCredentialPool,
					ArenaRequestHandler,
					SecureRandom));
		}

		private bool SanityCheck(IEnumerable<(Money Value, Script ScriptPubKey)> expectedOutputs, Transaction unsignedCoinJoinTransaction)
		{
			var coinJoinOutputs = unsignedCoinJoinTransaction.Outputs.Select(o => (o.Value, o.ScriptPubKey));
			return coinJoinOutputs.IsSuperSetOf(expectedOutputs);
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
	}
}
