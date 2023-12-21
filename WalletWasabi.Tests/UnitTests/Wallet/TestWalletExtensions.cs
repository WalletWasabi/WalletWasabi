using NBitcoin;
using System.Linq;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public static class TestWalletExtensions
{
	public static IDestination GetNextDestination(this TestWallet wallet) =>
		wallet.GetNextDestinations(1, false).Single();

	public static IDestination GetNextInternalDestination(this TestWallet wallet) =>
		wallet.GetNextInternalDestinations(1).Single();
}
