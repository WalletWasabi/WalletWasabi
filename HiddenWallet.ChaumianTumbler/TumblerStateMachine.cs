using HiddenWallet.ChaumianCoinJoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
    public class TumblerStateMachine : IDisposable
    {
		public string Phase { get; private set; } = TumblerPhase.InputRegistration;

		private CancellationTokenSource _ctsSourcePhaseCancel = new CancellationTokenSource();

		public void UpdatePhase(string phase)
		{
			if (phase == Phase) return;

			Phase = phase;
			_ctsSourcePhaseCancel.Cancel();
			_ctsSourcePhaseCancel = new CancellationTokenSource();
			Console.WriteLine($"NEW PHASE: {phase}");
		}

		public void AdvancePhase()
		{
			switch (Phase)
			{
				case TumblerPhase.InputRegistration:
					{
						UpdatePhase(TumblerPhase.InputConfirmation);
						break;
					}
				case TumblerPhase.InputConfirmation:
					{
						UpdatePhase(TumblerPhase.OutputRegistration);
						break;
					}
				case TumblerPhase.OutputRegistration:
					{
						UpdatePhase(TumblerPhase.Signing);
						break;
					}
				case TumblerPhase.Signing:
					{
						UpdatePhase(TumblerPhase.InputRegistration);
						break;
					}
				default:
					{
						throw new NotSupportedException("This should never happen");
					}
			}
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
								using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, _ctsSourcePhaseCancel.Token))
								{
									await Task.Delay(TimeSpan.FromSeconds(Global.Config.InputRegistrationPhaseTimeoutInSeconds), cts.Token).ContinueWith(t => { }); ;
								}
								AdvancePhase();
								break;
							}
						case TumblerPhase.InputConfirmation:
							{
								using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, _ctsSourcePhaseCancel.Token))
								{
									await Task.Delay(TimeSpan.FromSeconds(Global.Config.InputRegistrationPhaseTimeoutInSeconds), cts.Token).ContinueWith(t => { }); ;
								}
								AdvancePhase();
								break;
							}
						case TumblerPhase.OutputRegistration:
							{
								using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, _ctsSourcePhaseCancel.Token))
								{
									await Task.Delay(TimeSpan.FromSeconds(Global.Config.InputRegistrationPhaseTimeoutInSeconds), cts.Token).ContinueWith(t => { }); ;
								}
								AdvancePhase();
								break;
							}
						case TumblerPhase.Signing:
							{
								using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel, _ctsSourcePhaseCancel.Token))
								{
									await Task.Delay(TimeSpan.FromSeconds(Global.Config.InputRegistrationPhaseTimeoutInSeconds), cts.Token).ContinueWith(t => { }); ;
								}
								AdvancePhase();
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
					Console.WriteLine($"Ignoring {nameof(TumblerStateMachine)} exception: {ex}");
				}
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
					_ctsSourcePhaseCancel?.Dispose();
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
