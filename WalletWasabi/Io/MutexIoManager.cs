using Nito.AsyncEx;
using WalletWasabi.Crypto;

namespace WalletWasabi.Io
{
	public abstract class AsyncLockIoManager : IoManager
	{
		public AsyncLockIoManager(string filePath) : base(filePath)
		{
			AsyncLock = new AsyncLock();
		}

		public AsyncLock AsyncLock { get; }
	}
}
