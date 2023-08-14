using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using DynamicData;
using Moq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class ReceiveAddressViewModelTests
{
	[Fact]
	public void CopyCommandShouldSetAddressInClipboard()
	{
		var clipboard = Mock.Of<IClipboard>(MockBehavior.Loose);
		var context = new UiContextBuilder().WithClipboard(clipboard).Build();
		var sut = new ReceiveAddressViewModel(context, new TestWallet(), new TestAddress("SomeAddress"), false);

		sut.CopyAddressCommand.Execute(null);

		var mock = Mock.Get(clipboard);
		mock.Verify(x => x.SetTextAsync("SomeAddress"));
	}

	[Fact]
	public void AutoCopyEnabledShouldCopyToClipboard()
	{
		var clipboard = Mock.Of<IClipboard>(MockBehavior.Loose);
		var context = new UiContextBuilder().WithClipboard(clipboard).Build();
		new ReceiveAddressViewModel(context, new TestWallet(), new TestAddress("SomeAddress"), true);
		var mock = Mock.Get(clipboard);
		mock.Verify(x => x.SetTextAsync("SomeAddress"));
	}

	[Fact]
	public void WhenAddressBecomesUsedNavigationGoesBack()
	{
		var ns = Mock.Of<INavigationStack<RoutableViewModel>>(MockBehavior.Loose);
		var uiContext = Mocks.ContextWith(ns);
		var address = new TestAddress("SomeAddress");
		var wallet = WalletWithAddresses(address);
		var vm = new ReceiveAddressViewModel(uiContext, wallet, address, true);
		vm.OnNavigatedTo(false);

		address.IsUsed = true;

		Mock.Get(ns).Verify(x => x.Back(), Times.Once);
	}

	private static IWalletModel WalletWithAddresses(TestAddress address)
	{
		return Mock.Of<IWalletModel>(x => x.Addresses == AddressList(address).Connect(null).AutoRefresh(null, null, null));
	}

	private static ISourceCache<IAddress, string> AddressList(params IAddress[] addresses)
	{
		var cache = new SourceCache<IAddress, string>(s => s.Text);
		cache.PopulateFrom(addresses.ToObservable());
		return cache;
	}

	private class TestWallet : IWalletModel
	{
		public string Name => throw new NotSupportedException();

		public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions => throw new NotSupportedException();

		public IObservable<IChangeSet<IAddress, string>> Addresses => Observable.Empty<IChangeSet<IAddress, string>>();

		public IWalletBalancesModel Balances => throw new NotSupportedException();

		public bool IsLoggedIn => throw new NotSupportedException();

		public IObservable<WalletState> State => throw new NotSupportedException();

		bool IWalletModel.IsHardwareWallet => false;

		public bool IsWatchOnlyWallet => throw new NotSupportedException();

		public WalletType WalletType => throw new NotSupportedException();

		public IWalletAuthModel Auth => throw new NotImplementedException();

		public IWalletLoadWorkflow Loader => throw new NotImplementedException();

		public IWalletSettingsModel Settings => throw new NotImplementedException();

		public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
		{
			throw new NotSupportedException();
		}

		public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
		{
			throw new NotSupportedException();
		}

		public bool IsHardwareWallet()
		{
			return false;
		}

		public Task<WalletLoginResult> TryLoginAsync(string password)
		{
			throw new NotSupportedException();
		}

		public void Login()
		{
			throw new NotSupportedException();
		}

		public void Logout()
		{
			throw new NotSupportedException();
		}
	}
}
