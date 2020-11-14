using NBitcoin;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Tests.Helpers;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class TransactionStoreMock : TransactionStore
	{
		public TransactionStoreMock([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		{
			// Make sure starts with clear state.
			var filePath = Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName), "Transactions.dat");
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}
		}

		public async Task InitializeAsync(Network network, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		{
			var dir = Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName));
			await InitializeAsync(dir, network, $"{nameof(TransactionStoreMock)}.{nameof(TransactionStoreMock.InitializeAsync)}");
		}
	}
}
