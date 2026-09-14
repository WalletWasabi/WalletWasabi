using NBitcoin;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class FileSystemBlockRepositoryTests
{
	[Fact]
	public async Task PrunesFilesOverConfiguredSizeAsync()
	{
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);

		try
		{
			Directory.CreateDirectory(workDir);
			var oldFilePath = Path.Combine(workDir, "old-block");
			await File.WriteAllBytesAsync(oldFilePath, new byte[1024 * 1024]);
			File.SetLastAccessTimeUtc(oldFilePath, DateTime.UtcNow - TimeSpan.FromDays(1));

			var repository = new FileSystemBlockRepository(workDir, Network.Main, targetBlocksFolderSizeInMegabytes: 1);
			var block = Network.Main.Consensus.ConsensusFactory.CreateBlock();
			block.Header.Nonce = 1;

			await repository.SaveAsync(block, CancellationToken.None);

			Assert.False(File.Exists(oldFilePath));
			Assert.True(File.Exists(Path.Combine(workDir, block.GetHash().ToString())));
		}
		finally
		{
			await IoHelpers.TryDeleteDirectoryAsync(workDir);
		}
	}
}
