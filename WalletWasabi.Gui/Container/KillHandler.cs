using System.Threading;

namespace WalletWasabi.Gui.Container
{
	public class KillHandler
	{
		/// <summary>
		/// 0: nobody called
		/// 1: somebody called
		/// 2: call finished
		/// </summary>
		private long _dispose = 0; // To detect redundant calls

		public enum Status
		{
			NOBODY_CALLED = 0,
			SOMEBODY_CALLED = 1,
			CALL_FINISHED = 2
		}

		public bool KillRequested => Interlocked.Read(ref _dispose) > 0;

		public void SetStatus(Status state)
		{
			Interlocked.Exchange(ref _dispose, (long)state);
		}

		public Status CompareExchange(Status newState, Status comparand)
		{
			return (Status)Interlocked.CompareExchange(ref _dispose, (long)newState, (long)comparand);
		}

		public Status GetState()
		{
			return (Status)Interlocked.Read(ref _dispose);
		}
	}
}
