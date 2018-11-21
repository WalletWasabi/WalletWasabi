using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Backend
{
	public class CcjAnonSetTunner : IDisposable
	{
		private CancellationTokenSource _cts;

		private bool _running;
		private TimeSpan _interval;

		private static int completedRounds = 0;

		public CcjAnonSetTunner()
		{
			_running = false;
			_interval =  TimeSpan.FromDays(1);
			_cts = new CancellationTokenSource();
		}

		public void Start()
		{
			var startedTime = DateTime.UtcNow;
			_running = true;

			Task.Run(async () =>
			{
				while (_running)
				{
					// If stop was requested return.
					if (!_running) return;

					try
					{
						var _runningTime = DateTime.UtcNow - startedTime;
						if(_runningTime >= _interval)
						{
							if(completedRounds > Global.RoundConfig.ExpectedRoundsPerDay )
							{
								var newAnonymitySet = Math.Min((int)Global.RoundConfig.AnonymitySet + 1, 49);
								var oldAnonymitySet = Global.RoundConfig.AnonymitySet;
								if(newAnonymitySet > oldAnonymitySet)
								{ 
									Global.RoundConfig.AnonymitySet = newAnonymitySet; 
									Global.Coordinator.AbortAllRoundsInInputRegistration(nameof(CcjAnonSetTunner), $"Increase rounds anonymity set from {oldAnonymitySet} to {newAnonymitySet}");
								}
							}
							else if(completedRounds < Global.RoundConfig.ExpectedRoundsPerDay)
							{
								var newAnonymitySet = Math.Min((int)Global.RoundConfig.AnonymitySet - 1, 2);
								var oldAnonymitySet = Global.RoundConfig.AnonymitySet;
								if(newAnonymitySet < oldAnonymitySet)
								{ 
									Global.RoundConfig.AnonymitySet = newAnonymitySet; 
									Global.Coordinator.AbortAllRoundsInInputRegistration(nameof(CcjAnonSetTunner), $"Decrease rounds anonymity set from {oldAnonymitySet} to {newAnonymitySet}");
								}
							}
							else
							{
								Logger.LogInfo<CcjAnonSetTunner>($"There were {completedRounds} completed rounds in 24hrs. No anonymity set adjustments is required.");
							}
							completedRounds = 0;
						}
					}
					finally
					{
						if (_running)
						{
							try
							{
								await Task.Delay(_interval, _cts.Token);
							}
							catch (TaskCanceledException ex)
							{
								Logger.LogTrace<CcjAnonSetTunner>(ex);
							}
						}
					}
				}
			});
		}

		public void RoundCompleted()
		{
			completedRounds++;
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false;

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					if (_running)
					{
						_running = false;
					}
					_cts?.Cancel();
					_cts?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}