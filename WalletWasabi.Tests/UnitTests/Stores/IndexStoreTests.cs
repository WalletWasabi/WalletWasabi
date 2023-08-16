using NBitcoin;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Stores;

/// <summary>
/// Tests for <see cref="IndexStore"/>.
/// </summary>
public class IndexStoreTests
{
	[Fact]
	public async Task IndexStoreTestsAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));

		string directory = GetWorkDirectory();
		await IoHelpers.TryDeleteDirectoryAsync(directory);
		IoHelpers.EnsureContainingDirectoryExists(directory);

		await using var indexStore = new IndexStore(directory, Network.Main, new SmartHeaderChain());
		await indexStore.InitializeAsync(testDeadlineCts.Token);
	}

	private string GetWorkDirectory([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		=> Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName), "IndexStore");
}
