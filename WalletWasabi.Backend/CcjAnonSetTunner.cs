using System;
using System.Threading;

namespace WalletWasabi.Backend
{
	public class CcjAnonSetTunner : IDisposable
	{
		private static Timer _timer = new Timer(ControlRequiredAnonymitySet, null, Timeout.Infinite, Timeout.Infinite);		
		
		private static int completedRounds = 0;

		public void Start()
		{
			var interval = TimeSpan.FromHours(24);
			 _timer.Change(interval, interval);
		}

		public void Stop()
		{
			 _timer.Change(Timeout.Infinite, Timeout.Infinite);
		}

		public static void ControlRequiredAnonymitySet(object state)
		{
			if(completedRounds > Global.RoundConfig.ExpectedRoundsPerDay )
			{
				Global.RoundConfig.AnonymitySet = Math.Min((int)Global.RoundConfig.AnonymitySet + 1, 49);
				completedRounds = 0;
			}
			else if(completedRounds < Global.RoundConfig.ExpectedRoundsPerDay)
			{
				Global.RoundConfig.AnonymitySet = Math.Max((int)Global.RoundConfig.AnonymitySet - 1, 2);
				completedRounds = 0;
			}
		}

		public void ControlRequiredAnonymitySet()
		{
			ControlRequiredAnonymitySet(null);
		}

		public void RoundCompleted()
		{
			completedRounds++;
		}

		public void Dispose()
		{
			Stop();
			_timer.Dispose();	
		}
	}
}