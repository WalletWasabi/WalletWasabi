using NBitcoin;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class SynchronizerTests
{
	[Fact]
	public async Task AlreadyOnBestBlock_DoesNotTakeTheLocalTipForTheNetworkTipAsync()
	{
		string directory = GetWorkDirectory();
		await IoHelpers.TryDeleteDirectoryAsync(directory);

		var eventBus = new EventBus();
		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(FilterCheckpoints.GetCheckpointForBirthday(700_000u, Network.Main).Header);
		using var filterStore = new FilterStore(directory, Network.Main, filterHeaderChain, eventBus);

		var networkTipAnnounced = false;
		using var subscription = eventBus.Subscribe<NetworkTipHeightChanged>(_ => networkTipAnnounced = true);

		var isSynchronized = await Synchronizer.ProcessFiltersAsync(new FiltersResponse.AlreadyOnBestBlock(), filterStore, filterHeaderChain, eventBus);

		Assert.True(isSynchronized);
		Assert.Equal(Height.ChainHeight.Genesis, filterHeaderChain.ServerTipHeight);
		Assert.False(networkTipAnnounced);
	}

	private string GetWorkDirectory([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		=> Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName), "IndexStore");
}
