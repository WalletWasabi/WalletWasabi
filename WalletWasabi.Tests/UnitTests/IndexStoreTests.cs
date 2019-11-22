using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class IndexStoreTests
	{
		[Fact]
		public async Task IndexStoreTestsAsync()
		{
			var indexStore = new IndexStore();

			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName());
			Directory.Delete(dir, true);
			var network = Network.Main;
			await indexStore.InitializeAsync(dir, network, new SmartHeaderChain());
		}

		[Fact]
		public async Task InconsistentMatureIndexAsync()
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName(), "IndexStore");
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(dir);
			var matureFilters = Path.Combine(dir, "MatureIndex.dat");
			IoHelpers.EnsureContainingDirectoryExists(matureFilters);

			var indexStore = new IndexStore();

			var network = Network.Main;
			var headersChain = new SmartHeaderChain();

			var dummyFilter = GolombRiceFilter.Parse("00");
			DateTimeOffset minutesAgo(int mins) => DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(mins));
			var matureIndexStoreContent = new[]
			{
				new FilterModel(new SmartHeader(new uint256(2), new uint256(1), 1, minutesAgo(30)), dummyFilter),
				new FilterModel(new SmartHeader(new uint256(3), new uint256(2), 2, minutesAgo(20)), dummyFilter),
				new FilterModel(new SmartHeader(new uint256(99), new uint256(98), 98, minutesAgo(10)), dummyFilter)
			};
			await File.WriteAllLinesAsync(matureFilters, matureIndexStoreContent.Select(x => x.ToLine()));

			await Assert.ThrowsAsync<InvalidOperationException>(async () => await indexStore.InitializeAsync(dir, network, headersChain));
			Assert.Equal(new uint256(3), headersChain.TipHash);
			Assert.Equal(2u, headersChain.TipHeight);

			// Check if the matureIndex is deleted
			Assert.False(File.Exists(matureFilters));
		}

		[Fact]
		public async Task InconsistentImatureIndexAsyncAsync()
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName(), "IndexStore");
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(dir);
			var immatureFilters = Path.Combine(dir, "ImmatureIndex.dat");
			IoHelpers.EnsureContainingDirectoryExists(immatureFilters);

			var indexStore = new IndexStore();

			var network = Network.Main;
			var headersChain = new SmartHeaderChain();

			var dummyFilter = GolombRiceFilter.Parse("00");
			DateTimeOffset minutesAgo(int mins) => DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(mins));
			var startingFilter = StartingFilters.GetStartingFilter(network);

			var immatureIndexStoreContent = new[]
			{
				new FilterModel(new SmartHeader(new uint256(2), startingFilter.Header.BlockHash, startingFilter.Header.Height + 1, minutesAgo(30)), dummyFilter),
				new FilterModel(new SmartHeader(new uint256(3), new uint256(2), startingFilter.Header.Height + 2, minutesAgo(20)), dummyFilter),
				new FilterModel(new SmartHeader(new uint256(99), new uint256(98), startingFilter.Header.Height + 98, minutesAgo(10)), dummyFilter)
			};
			await File.WriteAllLinesAsync(immatureFilters, immatureIndexStoreContent.Select(x => x.ToLine()));

			await Assert.ThrowsAsync<InvalidOperationException>(async () => await indexStore.InitializeAsync(dir, network, headersChain));
			Assert.Equal(new uint256(3), headersChain.TipHash);
			Assert.Equal(startingFilter.Header.Height + 2u, headersChain.TipHeight);

			// Check if the matureIndex is deleted
			Assert.False(File.Exists(immatureFilters));
		}

		[Fact]
		public async Task GapInIndexAsyncAsync()
		{
			var dir = Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.GetMethodName(), "IndexStore");
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(dir);
			var matureFilters = Path.Combine(dir, "MatureIndex.dat");
			var immatureFilters = Path.Combine(dir, "ImmatureIndex.dat");
			IoHelpers.EnsureContainingDirectoryExists(matureFilters);
			IoHelpers.EnsureContainingDirectoryExists(immatureFilters);

			var indexStore = new IndexStore();

			var network = Network.Main;
			var headersChain = new SmartHeaderChain();

			var dummyFilter = GolombRiceFilter.Parse("00");
			DateTimeOffset minutesAgo(int mins) => DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(mins));
			var matureIndexStoreContent = new[]
			{
				new FilterModel(new SmartHeader(new uint256(2), new uint256(1), 1, minutesAgo(30)), dummyFilter),
				new FilterModel(new SmartHeader(new uint256(3), new uint256(2), 2, minutesAgo(20)), dummyFilter),
			};
			await File.WriteAllLinesAsync(matureFilters, matureIndexStoreContent.Select(x => x.ToLine()));
			var immatureIndexStoreContent = new[]
			{
				new FilterModel(new SmartHeader(new uint256(5), new uint256(4), 4, minutesAgo(30)), dummyFilter),
				new FilterModel(new SmartHeader(new uint256(6), new uint256(5), 5, minutesAgo(20)), dummyFilter),
			};
			await File.WriteAllLinesAsync(immatureFilters, immatureIndexStoreContent.Select(x => x.ToLine()));

			await Assert.ThrowsAsync<InvalidOperationException>(async () => await indexStore.InitializeAsync(dir, network, headersChain));
			Assert.Equal(new uint256(3), headersChain.TipHash);
			Assert.Equal(2u, headersChain.TipHeight);

			Assert.False(File.Exists(matureFilters));   // mature filters are ok but they are deleted anyway??
			Assert.False(File.Exists(immatureFilters)); // immature filters are NOT ok
		}
	}
}
