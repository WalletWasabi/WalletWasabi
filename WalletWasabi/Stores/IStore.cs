using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Stores
{
	public interface IStore
	{
		Task InitializeAsync(string workFolderPath, Network network, bool ensureBackwardsCompatibility);
	}
}
