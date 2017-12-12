using ConcurrentCollections;
using HiddenWallet.ChaumianCoinJoin;
using HiddenWallet.ChaumianCoinJoin.Models;
using HiddenWallet.ChaumianTumbler.Clients;
using HiddenWallet.ChaumianTumbler.Configuration;
using HiddenWallet.Helpers;
using HiddenWallet.WebClients.SmartBit;
using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
    public class TumblerStateMachine : IDisposable
    {
		public volatile TumblerPhase Phase;
		public volatile int RoundId;
		public volatile string RoundHash;

		public Money Denomination { get; private set; }
		public Money FeePerInputs { get; private set; }
		public Money FeePerOutputs { get; private set; }
		public int AnonymitySet { get; private set; } = (int)Global.Config.MinimumAnonymitySet;
		public TimeSpan TimeSpentInInputRegistration { get; private set; } = TimeSpan.FromSeconds((int)Global.Config.AverageTimeToSpendInInputRegistrationInSeconds) + TimeSpan.FromSeconds(1); // took one sec longer, so the first round will use the same anonymity set
		public volatile bool AcceptRequest;
		public Stopwatch InputRegistrationStopwatch { get; private set; }
		public volatile bool FallBackRound;

		public volatile Transaction CoinJoin;
		public volatile string UnsignedCoinJoinHex;
		public bool FullySignedCoinJoin => CoinJoin?.Inputs == null ? false : CoinJoin.Inputs.All(x => !string.IsNullOrWhiteSpace(x.WitScript?.ToString()));

		private SmartBitClient SmartBitClient { get; } = new SmartBitClient(Network.Main);
		public ConcurrentHashSet<Alice> Alices { get; private set; }
		public ConcurrentHashSet<Bob> Bobs { get; private set; }
		public ConcurrentHashSet<string> BlindedOutputs { get; private set; }

		private NotificationBroadcaster _broadcaster = NotificationBroadcaster.Instance;

		private CancellationTokenSource _ctsPhaseCancel = new CancellationTokenSource();

		public TumblerStateMachine()
		{
			Phase = TumblerPhase.InputRegistration;
			RoundId = 0;
			AcceptRequest = false;
			FallBackRound = false;
			CoinJoin = null;
			InputRegistrationStopwatch = new Stopwatch();
		}

		public async Task BroadcastPhaseChangeAsync()
		{
			AcceptRequest = true;
			var broadcast = new PhaseChangeBroadcast { NewPhase = Phase.ToString(), Message = "" };
			if (FallBackRound && Phase == TumblerPhase.InputRegistration)
			{
				broadcast = new PhaseChangeBroadcast { NewPhase = "FallBack" + Phase.ToString(), Message = "" };
			}
			await _broadcaster.BroadcastAsync(broadcast);
			Console.WriteLine($"NEW PHASE: {Phase}");
		}

		public async Task BroadcastPeerRegisteredAsync()
		{
			int numberOfPeers = Alices.Count;
			var broadcast = new PeerRegisteredBroadcast { NewRegistration = numberOfPeers, Message = ""};
			await _broadcaster.BroadcastAsync(broadcast);
			Console.WriteLine($"Number of peers: {numberOfPeers}");
		}

		public void UpdatePhase(TumblerPhase phase)
		{
			if (phase == Phase) return;
			AcceptRequest = false;

			Phase = phase;
			_ctsPhaseCancel.Cancel();
			_ctsPhaseCancel = new CancellationTokenSource();
		}

		public async Task StartAsync(CancellationToken cancel)
		{
			while (true)
			{
				try
				{
					if (cancel.IsCancellationRequested) return;

					switch(Phase)
					{
						case TumblerPhase.InputRegistration:
							{
								await Global.InitializeConfigAsync();
								await Global.InitializeUtxoRefereeAsync();

								RoundId++;
								Console.WriteLine($"New Round: {RoundId}");
								Alices = new ConcurrentHashSet<Alice>();
								BlindedOutputs = new ConcurrentHashSet<string>();
								await Global.StateMachine.BroadcastPeerRegisteredAsync();
								Bobs = new ConcurrentHashSet<Bob>();
								await SetDenominationAsync(cancel);
								await SetFeesAsync(cancel);
								CalculateAnonymitySet();

								await BroadcastPhaseChangeAsync();
								InputRegistrationStopwatch.Restart();
								using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, _ctsPhaseCancel.Token))
								{
									await Task.Delay(TimeSpan.FromSeconds((int)Global.Config.InputRegistrationPhaseTimeoutInSeconds), cts.Token).ContinueWith(t => { });
								}
								InputRegistrationStopwatch.Stop();
								if (FallBackRound == false)
								{
									TimeSpentInInputRegistration = InputRegistrationStopwatch.Elapsed;
								}

								UpdatePhase(TumblerPhase.ConnectionConfirmation);
								break;
							}
						case TumblerPhase.ConnectionConfirmation:
							{
								RoundHash = NBitcoinHelpers.HashOutpoints(Alices.SelectMany(x => x.Inputs).Select(x => x.OutPoint));

								await BroadcastPhaseChangeAsync();
								using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, _ctsPhaseCancel.Token))
								{
									await Task.Delay(TimeSpan.FromSeconds((int)Global.Config.ConnectionConfirmationPhaseTimeoutInSeconds), cts.Token).ContinueWith(t => { });
								}
								if (Alices.All(x => x.State == AliceState.ConnectionConfirmed))
								{
									UpdatePhase(TumblerPhase.OutputRegistration);
								}
								else
								{
									FallBackRound = true;
									UpdatePhase(TumblerPhase.InputRegistration);
								}
								break;
							}
						case TumblerPhase.OutputRegistration:
							{
								await BroadcastPhaseChangeAsync();
								using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, _ctsPhaseCancel.Token))
								{
									await Task.Delay(TimeSpan.FromSeconds((int)Global.Config.OutputRegistrationPhaseTimeoutInSeconds), cts.Token).ContinueWith(t => { });
								}
								// Output registration never falls back
								// We don't know which Alice to ban
								// Therefore proceed to signing, and whichever Alice doesn't sign ban
								UpdatePhase(TumblerPhase.Signing); 
								break;
							}
						case TumblerPhase.Signing:
							{
								BuildCoinJoin();
								
								await BroadcastPhaseChangeAsync();
								using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, _ctsPhaseCancel.Token))
								{
									await Task.Delay(TimeSpan.FromSeconds((int)Global.Config.SigningPhaseTimeoutInSeconds), cts.Token).ContinueWith(t => { });
								}
								if(!FullySignedCoinJoin)
								{
									FallBackRound = true;
									foreach(var alice in Alices)
									{
										if(alice.State != AliceState.SignedCoinJoin)
										{
											await Global.UtxoReferee.BanAliceAsync(alice);
										}
									}
								}
								UpdatePhase(TumblerPhase.InputRegistration);
								CoinJoin = null;
								break;
							}
						default:
							{
								throw new NotSupportedException("This should never happen");
							}
					}
				}
				catch (Exception ex)
				{
					FallBackRound = true;
					UpdatePhase(TumblerPhase.InputRegistration);
					Console.WriteLine($"Fallback to InputRegistration, Reason: {nameof(TumblerStateMachine)} exception: {ex}");
				}
			}
		}

		private void BuildCoinJoin()
		{
			var transaction = new Transaction();
			foreach (var bob in Bobs)
			{
				transaction.AddOutput(Denomination, bob.Output);
			}
			// if there are less bobs than alices add our own address (the client should refuse to sign this case and we should ban it)
			// this happens when alice did not provide output for the mix
			// if the wallet doesn't refuse it because of the bug and will just sign (eg. it wasn't an attack), then we can give back the money
			for (int i = 0; i < Alices.Count - Bobs.Count; i++)
			{
				var donation = Global.Config.Network == Network.Main ? new BitcoinWitPubKeyAddress("bc1q6y83sgk28p5jjaac4t9mcwj2jaj9fdetvynt29", Network.Main) : new BitcoinWitPubKeyAddress("tb1q6y83sgk28p5jjaac4t9mcwj2jaj9fdetxzgc3k", Network.TestNet);
				transaction.AddOutput(Denomination, donation);
			}

			foreach (var alice in Alices)
			{
				foreach (var input in alice.Inputs)
				{
					transaction.AddInput(new TxIn(input.OutPoint));
				}
				transaction.AddOutput(alice.ChangeAmount, alice.ChangeOutput);
			}

			var builder = new TransactionBuilder();
			CoinJoin = builder
				.ContinueToBuild(transaction)
				.Shuffle()
				.BuildTransaction(false);
			UnsignedCoinJoinHex = CoinJoin.ToHex();
		}

		private void CalculateAnonymitySet()
		{
			var min = (int)Global.Config.MinimumAnonymitySet;
			var max = (int)Global.Config.MaximumAnonymitySet;
			// If the previous non-fallback Input Registration phase took more than three minutes
			if (TimeSpentInInputRegistration > TimeSpan.FromSeconds((int)Global.Config.AverageTimeToSpendInInputRegistrationInSeconds))
			{
				// decrement this round's desired anonymity set relative to the previous desired anonymity set,
				var tmpAnonSet = Math.Max(min, AnonymitySet - 1);
				AnonymitySet = Math.Min(max, tmpAnonSet); // it happens if anonset constrains are modified on the fly
			}
			else
			{
				// otherwise increment it
				var tmpAnonSet = Math.Min(max, AnonymitySet + 1);
				AnonymitySet = Math.Max(min, tmpAnonSet); // it happens if anonset constrains are modified on the fly
			}
		}

		private async Task SetDenominationAsync(CancellationToken cancel)
		{
			if (Global.Config.DenominationAlgorithm == DenominationAlgorithm.FixedUSD)
			{
				try
				{
					var exchangeRates = await SmartBitClient.GetExchangeRatesAsync(cancel);
					decimal price = exchangeRates.Single(x => x.Code == "USD").Rate;
					decimal denominationUSD = (decimal)Global.Config.DenominationUSD;

					decimal denominationBTC = 0;
					var i = 1;
					while(denominationBTC == 0 && i <= 8)
					{
						denominationBTC = Math.Round(denominationUSD / price, i);
						i++;
					}
					Denomination = new Money(denominationBTC, MoneyUnit.BTC);
				}
				catch
				{
					// if denomination hasn't been initialized once, fall back to config data
					if (Denomination == null)
					{
						Denomination = Global.Config.DenominationBTC;
					}
				}
			}
			else if (Global.Config.DenominationAlgorithm == DenominationAlgorithm.FixedBTC)
			{
				Denomination = Global.Config.DenominationBTC;
			}
			else
			{
				throw new NotSupportedException(Global.Config.DenominationAlgorithm.ToString());
			}
		}

		private async Task SetFeesAsync(CancellationToken cancel)
		{
			EstimateInputAndOutputSizes(out int inputSizeInBytes, out int outputSizeInBytes);

			try
			{
				var estimateSmartFeeResponse = await Global.RpcClient.TryEstimateSmartFeeAsync((int)Global.Config.FeeConfirmationTarget, Global.Config.FeeEstimationMode);
				if (estimateSmartFeeResponse == null) throw new InvalidOperationException("FeeRate is not yet initialized");
				var feeRate = estimateSmartFeeResponse.FeeRate;
				Money feePerBytes = (feeRate.FeePerK / 1000);

				// make sure min relay fee (1000 sat) is hit
				FeePerInputs = Math.Max(feePerBytes * inputSizeInBytes, new Money(500));
				FeePerOutputs = Math.Max(feePerBytes * outputSizeInBytes, new Money(250));
			}
			catch
			{
				// if fee hasn't been initialized once, fall back
				if (FeePerInputs == null || FeePerOutputs == null)
				{
					var feePerBytes = new Money((int)Global.Config.FallBackSatoshiFeePerBytes);

					// make sure min relay fee (1000 sat) is hit
					FeePerInputs = Math.Max(feePerBytes * inputSizeInBytes, new Money(500));
					FeePerOutputs = Math.Max(feePerBytes * outputSizeInBytes, new Money(250));
				}
			}
		}

		public static void EstimateInputAndOutputSizes(out int inputSizeInBytes, out int outputSizeInBytes)
		{
			inputSizeInBytes = (int)Math.Ceiling(((3 * Constants.P2wpkhInputSizeInBytes) + Constants.P2pkhInputSizeInBytes) / 4m);
			outputSizeInBytes = Constants.OutputSizeInBytes;
		}

		private object _aliceLock = new object();
		public Alice FindAlice(string uniqueId, bool throwException)
		{
			lock (_aliceLock)
			{
				Alice alice = Alices.FirstOrDefault(x => x.UniqueId == new Guid(uniqueId));
				if (alice == default(Alice))
				{
					if (throwException)
					{
						throw new ArgumentException("Wrong uniqueId");
					}
				}

				return alice;
			}
		}

		public bool TryRemoveAlice(string uniqueId)
		{
			return TryRemoveAlice(new Guid(uniqueId));
		}
		public bool TryRemoveAlice(Guid uniqueId)
		{
			lock (_aliceLock)
			{
				Alice alice = Alices.FirstOrDefault(x => x.UniqueId == uniqueId);
				return Alices.TryRemove(alice);
			}
		}

		#region IDisposable Support
		private bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_ctsPhaseCancel?.Dispose();
					SmartBitClient?.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				_disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~TumblerStateMachine() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
