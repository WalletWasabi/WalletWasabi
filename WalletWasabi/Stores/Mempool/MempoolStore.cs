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
		public string WorkFolderPath { get; private set; }
		public Network Network { get; private set; }

		public MempoolStore()
		{
		}

		public void Initialize(string workFolderPath, Network network)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath, trim: true);
			Network = Guard.NotNull(nameof(network), network);
		}
	}
}
