using Nito.AsyncEx;
using WalletWasabi.Crypto;

namespace WalletWasabi.Io
{
	public abstract class MutexIoManager : IoManager
	{
		public MutexIoManager(string filePath) : base(filePath)
		{
			AsyncLock = new AsyncLock();
		}

		public AsyncLock AsyncLock { get; }
	}
}
