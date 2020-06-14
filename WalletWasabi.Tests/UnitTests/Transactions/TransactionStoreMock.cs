using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class TransactionStoreMock : TransactionStore
	{
		public TransactionStoreMock([CallerFilePath]string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
		{
			// Make sure starts with clear state.
			var filePath = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName, "Transactions.dat");
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}
		}

		public async Task InitializeAsync(Network network, [CallerFilePath]string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName);
			await InitializeAsync(dir, network, $"{nameof(TransactionStoreMock)}.{nameof(TransactionStoreMock.InitializeAsync)}");
		}
	}
}
