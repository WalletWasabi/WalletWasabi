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
	public class CoinJoinClient : BackgroundService, IDisposable
	{
		private bool _disposedValue;

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
			SecureRandom = new SecureRandom();
			Coins = coins;
		}

		private uint256 RoundId { get; }
		private RoundState RoundState { get; set; }
		private ZeroCredentialPool ZeroAmountCredentialPool { get; } = new();
		private ZeroCredentialPool ZeroVsizeCredentialPool { get; } = new();
		private SecureRandom SecureRandom { get; }
		private CancellationTokenSource DisposeCts { get; } = new();
		private IEnumerable<Coin> Coins { get; set; }
		private Random Random { get; } = new();
		public IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		public Kitchen Kitchen { get; }
		public KeyManager Keymanager { get; }

		protected override async Task ExecuteAsync(CancellationToken cancellationToken)
		{
			try
			{
				await RefreshRoundAsync(cancellationToken).ConfigureAwait(false);
				var constructionState = RoundState.Assert<ConstructionState>();

				// Calculate outputs values
				var outputValues = DecomposeAmounts();
				var outputs = outputValues.Zip(Keymanager.GetKeys(), (amount, hdPubKey) => new TxOut(amount, hdPubKey.P2wpkhScript));

				var plan = CreatePlan(
					Coins.Select(x => (ulong)x.Amount.Satoshi),
					Coins.Select(x => (ulong)x.ScriptPubKey.EstimateInputVsize()),
					outputValues);

				var aliceClients = CreateAliceClients();

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
			catch (Exception ex)
			{
				// The game is over for this round, no fallback mechanism. In the next round we will create another CoinJoinClient and try again.
			}
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
			var registerRequests = aliceClients.Select(alice => WrapCall(alice, alice.RegisterInputAsync(cancellationToken)));
			var completedRequests = await Task.WhenAll(registerRequests).ConfigureAwait(false);

			foreach (var request in completedRequests.Where(x => !x.Success))
			{
				Logger.LogWarning($"Round ({RoundState.Id}), Alice ({request.Sender.AliceId}): {nameof(AliceClient.RegisterInputAsync)} failed, reason:'{request.Exception}'.");
			}
			return completedRequests.Where(x => x.Success).Select(x => x.Sender).ToList();
		}

		private async Task<List<AliceClient>> ConfirmConnectionsAsync(IEnumerable<AliceClient> aliceClients, CancellationToken cancellationToken)
		{
			var confirmationRequests = aliceClients.Select(alice => WrapCall(alice, alice.ConfirmConnectionAsync(TimeSpan.FromMilliseconds(Random.Next(100, 1_000)), cancellationToken))).ToArray();
			var completedRequests = await Task.WhenAll(confirmationRequests).ConfigureAwait(false);

			foreach (var request in completedRequests.Where(x => !x.Success))
			{
				Logger.LogWarning($"Round ({RoundState.Id}), Alice ({request.Sender.AliceId}): {nameof(AliceClient.ConfirmConnectionAsync)} failed, reason:'{request.Exception}'.");
			}

			return completedRequests.Where(x => x.Success).Select(x => x.Sender).ToList();
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
            var bobClients = Enumerable.Range(0, int.MaxValue).Select(_ => CreateBobClient());
			var outputRegistrationData = outputsWithCredentials.Zip(
                    bobClients, 
                    (o, b) => (
                        TxOut: o.Output,
                        RealAmountCredentials: o.RealAmountCredentials,
                        RealVsizeCredentials: o.RealVsizeCredentials,
                        BobClient: b));

			var outputRegisterRequests = outputRegistrationData
				.Select(x => WrapCall(x.TxOut, x.BobClient.RegisterOutputAsync(x.TxOut.Value, x.TxOut.ScriptPubKey, x.RealAmountCredentials, x.RealVsizeCredentials, cancellationToken)));
			var completedRequests = await Task.WhenAll(outputRegisterRequests).ConfigureAwait(false);

			foreach (var request in completedRequests.Where(x => !x.Success))
			{
				Logger.LogWarning($"Round ({RoundState.Id}), Bob ({request.Sender.ScriptPubKey}): {nameof(BobClient.RegisterOutputAsync)} failed, reason:'{request.Exception}'.");
			}
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

		public async override Task StartAsync(CancellationToken cancellationToken)
		{
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(DisposeCts.Token, cancellationToken);
			await base.StartAsync(linkedCts.Token).ConfigureAwait(false);
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
			foreach (var aliceClient in aliceClients)
			{
				await aliceClient.SignTransactionAsync(unsignedCoinJoinTransaction, cancellationToken).ConfigureAwait(false);
			}
		}

		private async Task<(bool Success, TSender Sender, Exception? Exception)> WrapCall<TSender>(TSender sender, Task task)
		{
			try
			{
				await task.ConfigureAwait(false);
				return (true, sender, default);
			}
			catch (Exception e)
			{
				return (false, sender, e);
			}
		}

		public async override Task StopAsync(CancellationToken cancellationToken)
		{
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(DisposeCts.Token, cancellationToken);
			await base.StopAsync(linkedCts.Token).ConfigureAwait(false);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					DisposeCts.Cancel();
					SecureRandom.Dispose();
				}
				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
