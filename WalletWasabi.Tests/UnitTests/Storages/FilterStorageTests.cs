using NBitcoin;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Storages;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Storages;

/// <summary>
/// Tests for <see cref="FilterStorage"/>.
/// </summary>
public class FilterStorageTests
{
	[Fact]
	public async Task FilterStorageTestsAsync()
	{
		using CancellationTokenSource testCts = new(TimeSpan.FromMinutes(1));

		string directory = GetWorkDirectory();
		await IoHelpers.TryDeleteDirectoryAsync(directory);
		IoHelpers.EnsureContainingDirectoryExists(directory);

		using var filterStorage = new FilterStorage(directory, Network.Main, new FilterHeaderChain(), new EventBus());
		await filterStorage.InitializeAsync(0, testCts.Token);

		// Remove starting filter.
		FilterModel? filterModel = await filterStorage.TryRemoveLastFilterAsync();
		Assert.NotNull(filterModel);

		// No filter to remove.
		filterModel = await filterStorage.TryRemoveLastFilterAsync();
		Assert.Null(filterModel);
	}

	private string GetWorkDirectory([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		=> Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName), "IndexStore");
}
