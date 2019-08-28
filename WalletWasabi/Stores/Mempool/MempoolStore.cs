using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Stores.Mempool
{
	public class MempoolStore
	{
		public Network Network { get; private set; }

		public MempoolStore()
		{
		}

		public void Initialize(Network network)
		{
			Network = Guard.NotNull(nameof(network), network);
		}
	}
}
