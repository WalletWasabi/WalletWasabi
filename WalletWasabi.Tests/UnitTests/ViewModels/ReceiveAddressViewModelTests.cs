using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Linq;
using DynamicData;
using Moq;
using NBitcoin;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
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
		var clipboard = Mock.Of<IUiClipboard>(MockBehavior.Loose);
		var context = new UiContextBuilder().WithClipboard(clipboard).Build();
		var sut = new ReceiveAddressViewModel(context, new TestWallet(), new TestAddress("SomeAddress"), false);

		sut.CopyAddressCommand.Execute(null);

		var mock = Mock.Get(clipboard);
		mock.Verify(x => x.SetTextAsync("SomeAddress"));
	}

	[Fact]
	public void AutoCopyEnabledShouldCopyToClipboard()
	{
		var clipboard = Mock.Of<IUiClipboard>(MockBehavior.Loose);
		var context = new UiContextBuilder().WithClipboard(clipboard).Build();
		new ReceiveAddressViewModel(context, new TestWallet(), new TestAddress("SomeAddress"), true);
		var mock = Mock.Get(clipboard);
		mock.Verify(x => x.SetTextAsync("SomeAddress"));
	}

	[Fact]
	public void WhenAddressBecomesUsedNavigationGoesBack()
	{
		var ns = Mock.Of<INavigationStack<RoutableViewModel>>(MockBehavior.Loose);
		var uiContext = MockUtils.ContextWith(ns);
		var address = new TestAddress("SomeAddress");
		var wallet = WalletWithAddresses(address);
		var vm = new ReceiveAddressViewModel(uiContext, wallet, address, true);
		vm.OnNavigatedTo(false);

		address.IsUsed = true;

		Mock.Get(ns).Verify(x => x.Back(), Times.Once);
	}

	private static IWalletModel WalletWithAddresses(IAddress address)
	{
		return new AddressTestingMocks.TestWallet(new[] { address }.AsObservableChangeSet(x => x.Text));
	}

	private class TestWallet : IWalletModel
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		public IAddressesModel AddressesModel => throw new NotSupportedException();
		public string Name => throw new NotSupportedException();
		public IObservable<IChangeSet<IAddress, string>> Addresses => Observable.Empty<IChangeSet<IAddress, string>>();
		public IObservable<WalletState> State => throw new NotSupportedException();
		bool IWalletModel.IsHardwareWallet => false;
		public bool IsWatchOnlyWallet => throw new NotSupportedException();
		public IWalletAuthModel Auth => throw new NotSupportedException();
		public IWalletLoadWorkflow Loader => throw new NotSupportedException();
		public IWalletSettingsModel Settings => throw new NotSupportedException();
		public IObservable<bool> HasBalance => throw new NotSupportedException();
		public IWalletPrivacyModel Privacy => throw new NotSupportedException();
		public IWalletCoinjoinModel Coinjoin => throw new NotSupportedException();
		public IObservable<Amount> Balances => throw new NotSupportedException();
		IWalletCoinsModel IWalletModel.Coins => throw new NotSupportedException();
		public Network Network => throw new NotSupportedException();
		IWalletTransactionsModel IWalletModel.Transactions => throw new NotSupportedException();
		public IAmountProvider AmountProvider => throw new NotSupportedException();

		public bool IsLoggedIn { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
		{
			throw new NotSupportedException();
		}

		public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
		{
			throw new NotSupportedException();
		}

		public IWalletInfoModel GetWalletInfo()
		{
			throw new NotSupportedException();
		}

		public IWalletStatsModel GetWalletStats()
		{
			throw new NotImplementedException();
		}
	}
}
