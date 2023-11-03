using System.Linq;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class ReceiveViewModelTests
{
	[Fact]
	public void EmptyAddressList()
	{
		Assert.False(HasUnusedAddresses(_ => { }));
	}

	[Fact]
	public void UsedAddress()
	{
		Assert.False(HasUnusedAddresses(configuration => configuration.SetUsed("addr")));
	}

	[Fact]
	public void UnusedAddress()
	{
		Assert.True(HasUnusedAddresses(configuration => configuration.SetUnused("addr")));
	}

	[Fact]
	public void UnusedBecomesUsed()
	{
		Assert.False(
			HasUnusedAddresses(
			configuration =>
			{
				configuration.SetUnused("addr");
				configuration.SetUsed("addr");
			}));
	}

	private static bool HasUnusedAddresses(Action<AddressTestingMocks.AddressConfiguration> configureAddresses)
	{
		var addresses = new AddressTestingMocks.AddressConfiguration();
		var receiveViewModel = new ReceiveViewModel(MockUtils.ContextStub(), new AddressTestingMocks.TestWallet(addresses.Addresses));
		var history = receiveViewModel.HasUnusedAddresses.SubscribeList();
		configureAddresses(addresses);
		return history.Last();
	}
}
