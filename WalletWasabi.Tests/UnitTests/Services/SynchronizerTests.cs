using NBitcoin;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Storages;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class SynchronizerTests
{
	/// <summary>
	/// <see cref="FiltersResponse.AlreadyOnBestBlock"/> message should not change the <see cref="FilterHeaderChain"/>'s server tip height
	/// and should not announce a network tip height change.
	/// </summary>
	[Fact]
	public async Task ProcessFiltersAsync_HandleAlreadyOnBestBlockMessageAsync()
	{
		var directory = await Common.GetEmptyWorkDirAsync();

		var eventBus = new EventBus();
		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(FilterCheckpoints.GetCheckpointForBirthday(700_000u, Network.Main).Header);
		using var filterManager = new FilterManager(directory, Network.Main, filterHeaderChain, eventBus);

		var networkTipAnnounced = false;
		using var _ = eventBus.Subscribe<NetworkTipHeightChanged>(_ => networkTipAnnounced = true);

		var isSynchronized = await Synchronizer.ProcessFiltersAsync(new FiltersResponse.AlreadyOnBestBlock(), filterManager, filterHeaderChain, eventBus);
		Assert.True(isSynchronized);
		Assert.Equal(Height.ChainHeight.Genesis, filterHeaderChain.ServerTipHeight);
		Assert.False(networkTipAnnounced);
	}
}
