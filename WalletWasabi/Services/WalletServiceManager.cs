using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class WalletServiceManager
	{
		private List<WalletService> WalletServices { get; }
		private object WalletServicesLock { get; }

		public WalletServiceManager()
		{
			WalletServices = new List<WalletService>();
			WalletServicesLock = new object();
		}

		public void AddWalletService(WalletService walletService)
		{
			lock (WalletServicesLock)
			{
				WalletServices.Add(walletService);
			}
		}

		public void RemoveWalletService(WalletService walletService)
		{
			lock (WalletServicesLock)
			{
				WalletServices.Remove(walletService);
			}
		}

		public IEnumerable<WalletService> GetWalletServices()
		{
			lock (WalletServicesLock)
			{
				return WalletServices.Where(x => x is { IsDisposed: var isDisposed } && !isDisposed).ToList();
			}
		}

		public bool AnyWalletService()
		{
			lock (WalletServicesLock)
			{
				return WalletServices.Where(x => x is { IsDisposed: var isDisposed } && !isDisposed).Any();
			}
		}
	}
}
