using NBitcoin;
using NBitcoin.DataEncoders;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets;

/// <summary>
/// FileSystemBlockRepository is a blocks repository that keeps the blocks in the file system.
/// </summary>
public class FileSystemBlockRepository : IRepository<uint256, Block>
{
	private const double MegaByte = 1024 * 1024;

	public FileSystemBlockRepository(string blocksFolderPath, Network network, long targetBlocksFolderSizeMb = 300)
	{
		using (BenchmarkLogger.Measure())
		{
			BlocksFolderPath = blocksFolderPath;
			Network = network;
			CreateFolders();
			EnsureBackwardsCompatibility();
			Prune(targetBlocksFolderSizeMb);
		}
	}

	public string BlocksFolderPath { get; }
	private Network Network { get; }
	private AsyncLock BlockFolderLock { get; } = new AsyncLock();

	/// <summary>
	/// Copies files one by one from <c>BlocksNETWORK_NAME</c> folder to <c>BitcoinStore/NETWORK_NAME/Blocks</c> if not already migrated.
	/// </summary>
	private void EnsureBackwardsCompatibility()
	{
		Logger.LogTrace(">");

		try
		{
			string dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
			string wrongGlobalBlockFolderPath = Path.Combine(dataDir, "Blocks");
			string[] wrongBlockFolderPaths = new[]
			{
					// Before Wasabi 1.1.13
					Path.Combine(dataDir, $"Blocks{Network}"),
					Path.Combine(wrongGlobalBlockFolderPath, Network.Name)
				};

			foreach (string wrongBlockFolderPath in wrongBlockFolderPaths.Where(x => Directory.Exists(x)))
			{
				MigrateBlocks(wrongBlockFolderPath);
			}

			if (Directory.Exists(wrongGlobalBlockFolderPath))
			{
				// If all networks successfully migrated, too, then delete the transactions folder, too.
				if (!Directory.EnumerateFileSystemEntries(wrongGlobalBlockFolderPath).Any())
				{
					Directory.Delete(wrongGlobalBlockFolderPath, recursive: true);
					Logger.LogInfo($"Deleted '{wrongGlobalBlockFolderPath}' folder.");
				}
				else
				{
					Logger.LogTrace($"Cannot delete '{wrongGlobalBlockFolderPath}' folder as it is not empty.");
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Backwards compatibility could not be ensured.");
			Logger.LogWarning(ex);
		}

		Logger.LogTrace("<");
	}

	private void MigrateBlocks(string blockFolderPath)
	{
		Logger.LogTrace($"Initiate migration of '{blockFolderPath}'");

		int cntSuccess = 0;
		int cntRedundant = 0;
		int cntFailure = 0;

		foreach (string oldBlockFilePath in Directory.EnumerateFiles(blockFolderPath))
		{
			try
			{
				MigrateBlock(oldBlockFilePath, ref cntSuccess, ref cntRedundant);
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"'{oldBlockFilePath}' failed to migrate.");
				Logger.LogDebug(ex);
				cntFailure++;
			}
		}

		Directory.Delete(blockFolderPath, recursive: true);

		if (cntSuccess > 0)
		{
			Logger.LogInfo($"Successfully migrated {cntSuccess} blocks to '{BlocksFolderPath}'.");
		}

		if (cntRedundant > 0)
		{
			Logger.LogInfo($"{cntRedundant} blocks were already in '{BlocksFolderPath}'.");
		}

		if (cntFailure > 0)
		{
			Logger.LogDebug($"Failed to migrate {cntFailure} blocks to '{BlocksFolderPath}'.");
		}

		Logger.LogInfo($"Deleted '{blockFolderPath}' folder.");
	}

	private void MigrateBlock(string blockFilePath, ref int cntSuccess, ref int cntRedundant)
	{
		string fileName = Path.GetFileName(blockFilePath);
		string newFilePath = Path.Combine(BlocksFolderPath, fileName);

		if (!File.Exists(newFilePath))
		{
			Logger.LogTrace($"Migrate '{blockFilePath}' -> '{newFilePath}'.");

			// Unintuitively File.Move overwrite: false throws an IOException if the file already exists.
			// https://docs.microsoft.com/en-us/dotnet/api/system.io.file.move?view=netcore-3.1
			File.Move(sourceFileName: blockFilePath, destFileName: newFilePath, overwrite: false);
			cntSuccess++;
		}
		else
		{
			Logger.LogTrace($"'{newFilePath}' already exists. Skip migrating.");
			cntRedundant++;
		}
	}

	/// <summary>
	/// Prunes <see cref="BlocksFolderPath"/> so that its size is at most <paramref name="maxFolderSizeMb"/> MB.
	/// </summary>
	/// <param name="maxFolderSizeMb">Max size of folder in mega bytes.</param>
	private void Prune(long maxFolderSizeMb)
	{
		Logger.LogTrace($"> {nameof(maxFolderSizeMb)}={maxFolderSizeMb}");

		try
		{
			List<FileInfo> fileInfoList = Directory.EnumerateFiles(BlocksFolderPath).Select(x => new FileInfo(x)).ToList();

			// Invalidate file info cache as per:
			// https://docs.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.lastaccesstimeutc?view=netcore-3.1#remarks
			fileInfoList.ForEach(x => x.Refresh());

			double sizeSumMb = 0;
			int cntPruned = 0;

			foreach (FileInfo blockFile in fileInfoList.OrderByDescending(x => x.LastAccessTimeUtc))
			{
				try
				{
					double fileSizeMb = blockFile.Length / MegaByte;

					if (sizeSumMb + fileSizeMb <= maxFolderSizeMb) // The file can stay stored.
					{
						sizeSumMb += fileSizeMb;
					}
					else if (sizeSumMb + fileSizeMb > maxFolderSizeMb) // Keeping the file would exceed the limit.
					{
						string blockHash = Path.GetFileNameWithoutExtension(blockFile.Name);
						blockFile.Delete();
						Logger.LogTrace($"Pruned {blockHash}. {nameof(sizeSumMb)}={sizeSumMb}.");
						cntPruned++;
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
				}
			}

			if (cntPruned > 0)
			{
				Logger.LogInfo($"Blocks folder was over {maxFolderSizeMb} MB. Deleted {cntPruned} blocks.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}

		Logger.LogTrace($"<");
	}

	/// <summary>
	/// Gets a bitcoin block from the file system.
	/// </summary>
	/// <param name="hash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public async Task<Block?> TryGetAsync(uint256 hash, CancellationToken cancellationToken)
	{
		// Try get the block.
		Block? block = null;
		using (await BlockFolderLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			var encoder = new HexEncoder();
			var filePath = Path.Combine(BlocksFolderPath, hash.ToString());
			if (File.Exists(filePath))
			{
				try
				{
					byte[] blockBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
					block = Block.Load(blockBytes, Network);

					_ = new FileInfo(filePath)
					{
						LastAccessTimeUtc = DateTime.UtcNow
					};
				}
				catch
				{
					// In case the block file is corrupted and we get an EndOfStreamException exception
					// Ignore any error and continue to re-downloading the block.
					Logger.LogDebug($"Block {hash} file corrupted, deleting file and block will be re-downloaded.");
					File.Delete(filePath);
				}
			}
		}

		return block;
	}

	/// <summary>
	/// Saves a bitcoin block in the file system.
	/// </summary>
	/// <param name="block">The block to be persisted in the file system.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task SaveAsync(Block block, CancellationToken cancellationToken)
	{
		var path = Path.Combine(BlocksFolderPath, block.GetHash().ToString());
		if (!File.Exists(path))
		{
			using (await BlockFolderLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!File.Exists(path))
				{
					await File.WriteAllBytesAsync(path, block.ToBytes(), CancellationToken.None).ConfigureAwait(false);
				}
			}
		}
	}

	/// <summary>
	/// Deletes a bitcoin block from the file system.
	/// </summary>
	/// <param name="hash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task RemoveAsync(uint256 hash, CancellationToken cancellationToken)
	{
		try
		{
			using (await BlockFolderLock.LockAsync(cancellationToken).ConfigureAwait(false))
			{
				var filePaths = Directory.EnumerateFiles(BlocksFolderPath);
				var fileNames = filePaths.Select(Path.GetFileName);
				var hashes = fileNames.Select(x => new uint256(x));

				if (hashes.Contains(hash))
				{
					File.Delete(Path.Combine(BlocksFolderPath, hash.ToString()));
				}
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	/// <summary>
	/// Returns the number of blocks available in the file system. (for testing only)
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public async Task<int> CountAsync(CancellationToken cancellationToken)
	{
		using (await BlockFolderLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			return Directory.EnumerateFiles(BlocksFolderPath).Count();
		}
	}

	private void CreateFolders()
	{
		try
		{
			if (Directory.Exists(BlocksFolderPath) && Network == Network.RegTest)
			{
				Directory.Delete(BlocksFolderPath, true);
			}
		}
		catch (Exception ex)
		{
			Logger.LogDebug(ex);
		}

		IoHelpers.EnsureDirectoryExists(BlocksFolderPath);
	}
}
