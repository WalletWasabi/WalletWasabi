using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Stores;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class TransactionStoreMock : TransactionStore
	{
		public TransactionStoreMock([CallerMemberName] string caller = null)
		{
			// Make sure starts with clear state.
			var filePath = Path.Combine(Global.Instance.DataDir, caller, "Transactions.dat");
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}
		}

		public async Task InitializeAsync(Network network, [CallerMemberName] string caller = null)
		{
			var dir = Path.Combine(Global.Instance.DataDir, caller);
			await InitializeAsync(dir, network, $"{nameof(TransactionStoreMock)}.{nameof(TransactionStoreMock.InitializeAsync)}");
		}
	}
}
