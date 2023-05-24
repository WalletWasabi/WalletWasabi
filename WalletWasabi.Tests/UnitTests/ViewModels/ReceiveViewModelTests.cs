// TODO: UI Decoupling
//using System.Collections.Generic;
//using System.Collections.Immutable;
//using System.Linq;
//using DynamicData;
//using FluentAssertions;
//using NBitcoin;
//using WalletWasabi.Fluent.Models.Wallets;
//using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
//using WalletWasabi.Fluent.ViewModels.Wallets.Receive;
//using WalletWasabi.Tests.UnitTests.ViewModels.TestDoubles;
//using Xunit;
//using TransactionSummary = WalletWasabi.Blockchain.Transactions.TransactionSummary;

//namespace WalletWasabi.Tests.UnitTests.ViewModels;

//public class ReceiveViewModelTests
//{
//	[Fact]
//	public void Empty_address_list()
//	{
//		HasUnusedAddresses(_ => { }).Should().BeFalse();
//	}

//	[Fact]
//	public void Used_address()
//	{
//		HasUnusedAddresses(configuration => configuration.SetUsed("addr")).Should().BeFalse();
//	}

//	[Fact]
//	public void Unused_address()
//	{
//		HasUnusedAddresses(configuration => { configuration.SetUnused("addr"); }).Should().BeTrue();
//	}

//	[Fact]
//	public void Unused_becomes_used()
//	{
//		HasUnusedAddresses(
//			configuration =>
//			{
//				configuration.SetUnused("addr");
//				configuration.SetUsed("addr");
//			}).Should().BeFalse();
//	}

//	private static bool HasUnusedAddresses(Action<AddressConfiguration> configureAddresses)
//	{
//		var addresses = new AddressConfiguration();
//		var receiveViewModel = new ReceiveViewModel(Mocks.Context(), new TestWallet(addresses.Cache));
//		var history = receiveViewModel.HasUnusedAddresses.SubscribeList();
//		configureAddresses(addresses);
//		return history.Last();
//	}

//	private class TestWallet : IWalletModel
//	{
//		public TestWallet(IConnectableCache<IAddress, string> addresses)
//		{
//			Addresses = addresses.Connect();
//		}

//		public string Name { get; }

//		public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

//		public IObservable<Money> Balance { get; }

//		public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

//		public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
//		{
//			throw new NotSupportedException();
//		}

//		public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
//		{
//			return ImmutableArray<(string Label, int Score)>.Empty;
//		}

//		public bool IsHardwareWallet()
//		{
//			return false;
//		}
//	}
	
//	private class AddressConfiguration
//	{
//		private readonly SourceCache<IAddress, string> _cache;

//		public AddressConfiguration()
//		{
//			_cache = new SourceCache<IAddress, string>(address => address.Text);
//		}

//		public IConnectableCache<IAddress, string> Cache => _cache;

//		public void SetUnused(string address)
//		{
//			_cache.AddOrUpdate(new TestAddress(address) { IsUsed = false });
//		}

//		public void SetUsed(string address)
//		{
//			_cache.AddOrUpdate(new TestAddress(address) { IsUsed = true });	
//		}
//	}
//}
