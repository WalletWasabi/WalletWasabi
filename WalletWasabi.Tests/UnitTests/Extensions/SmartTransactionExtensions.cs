using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Tests.UnitTests.Extensions;

public static class SmartTransactionExtensions
{
	public static bool IsRBF(this SmartTransaction tx) => !tx.Confirmed;
}
