using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;

internal class AddressTestingMocks
{
	public class TestWallet : IWalletModel
	{
		private readonly IObservableCache<IAddress, string> _addresses;

		public TestWallet(IObservableCache<IAddress, string> addresses)
		{
			_addresses = addresses;
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public IAddressesModel AddressesModel => new TestAddressesModel(_addresses);
		public WalletId Id { get; }
		public string Name { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public IObservable<WalletState> State => throw new NotSupportedException();
		public bool IsHardwareWallet => false;
		public bool IsWatchOnlyWallet => throw new NotSupportedException();
		public bool IsLoggedIn { get; set; }
		public IWalletAuthModel Auth => throw new NotSupportedException();
		public IObservable<bool> HasBalance => throw new NotSupportedException();
		public IWalletLoadWorkflow Loader => throw new NotSupportedException();
		public IWalletSettingsModel Settings => throw new NotSupportedException();
		public IWalletPrivacyModel Privacy => throw new NotSupportedException();
		public IWalletCoinjoinModel Coinjoin => throw new NotSupportedException();
		public IObservable<Amount> Balances => throw new NotSupportedException();
		IWalletCoinsModel IWalletModel.Coins => throw new NotSupportedException();
		public Network Network => throw new NotSupportedException();
		IWalletTransactionsModel IWalletModel.Transactions => throw new NotSupportedException();
		public IAmountProvider AmountProvider => throw new NotSupportedException();

		public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
		{
			throw new NotSupportedException();
		}

		public void Rename(string newWalletName) => throw new NotSupportedException();

		public IWalletStatsModel GetWalletStats() => throw new NotSupportedException();

		public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
		{
			return ImmutableArray<(string Label, int Score)>.Empty;
		}

		public IWalletInfoModel GetWalletInfo()
		{
			throw new NotSupportedException();
		}
	}

	public class AddressConfiguration
	{
		private readonly SourceCache<IAddress, string> _cache;

		public AddressConfiguration()
		{
			_cache = new SourceCache<IAddress, string>(address => address.Text);
		}

		public ISourceCache<IAddress, string> Addresses => _cache;

		public void SetUnused(string address)
		{
			_cache.AddOrUpdate(new TestAddress(address) { IsUsed = false });
		}

		public void SetUsed(string address)
		{
			_cache.AddOrUpdate(new TestAddress(address) { IsUsed = true });
		}
	}

	private class TestAddressesModel : IAddressesModel
	{
		public TestAddressesModel(IObservableCache<IAddress, string> cache)
		{
			Cache = cache;
			UnusedAddressesCache = Cache.Connect().AutoRefresh(x => x.IsUsed).Filter(address => !address.IsUsed).AsObservableCache();
			HasUnusedAddresses = UnusedAddressesCache.NotEmpty();
		}

		public IObservableCache<IAddress, string> UnusedAddressesCache { get; set; }
		public IObservableCache<IAddress, string> Cache { get; }
		public IObservable<bool> HasUnusedAddresses { get; }

		public void Dispose()
		{
		}
	}
}
