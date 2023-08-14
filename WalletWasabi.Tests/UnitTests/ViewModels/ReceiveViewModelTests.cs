using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
using WalletWasabi.Wallets;
using Xunit;
using TransactionSummary = WalletWasabi.Blockchain.Transactions.TransactionSummary;

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
		Assert.True(HasUnusedAddresses(configuration => { configuration.SetUnused("addr"); }));
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

	private static bool HasUnusedAddresses(Action<AddressConfiguration> configureAddresses)
	{
		var addresses = new AddressConfiguration();
		var receiveViewModel = new ReceiveViewModel(Mocks.ContextStub(), new TestWallet(addresses.Cache));
		var history = receiveViewModel.HasUnusedAddresses.SubscribeList();
		configureAddresses(addresses);
		return history.Last();
	}

	private class TestWallet : IWalletModel
	{
		public TestWallet(IConnectableCache<IAddress, string> addresses)
		{
			Addresses = addresses.Connect();
		}

		public string Name => throw new NotSupportedException();
		public bool IsLoggedIn => throw new NotSupportedException();

		public IObservable<WalletState> State => throw new NotSupportedException();

		public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions => throw new NotSupportedException();

		public IObservable<IChangeSet<IAddress, string>> Addresses { get; }
		public bool IsHardwareWallet => throw new NotSupportedException();

		public bool IsWatchOnlyWallet => throw new NotSupportedException();

		public WalletType WalletType => throw new NotSupportedException();

		public IWalletBalancesModel Balances => throw new NotSupportedException();
		public IWalletAuthModel Auth => throw new NotImplementedException();

		public IWalletLoadWorkflow Loader => throw new NotImplementedException();

		public IWalletSettingsModel Settings => throw new NotImplementedException();

		public IObservable<IChangeSet<ICoinModel>> Coins => throw new NotImplementedException();

		public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
		{
			throw new NotSupportedException();
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

		public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
		{
			return ImmutableArray<(string Label, int Score)>.Empty;
		}

		public IWalletInfoModel GetWalletInfo()
		{
			throw new NotImplementedException();
		}
	}

	private class AddressConfiguration
	{
		private readonly SourceCache<IAddress, string> _cache;

		public AddressConfiguration()
		{
			_cache = new SourceCache<IAddress, string>(address => address.Text);
		}

		public IConnectableCache<IAddress, string> Cache => _cache;

		public void SetUnused(string address)
		{
			_cache.AddOrUpdate(new TestAddress(address) { IsUsed = false });
		}

		public void SetUsed(string address)
		{
			_cache.AddOrUpdate(new TestAddress(address) { IsUsed = true });
		}
	}
}
