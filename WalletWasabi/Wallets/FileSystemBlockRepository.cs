using NBitcoin;
using NBitcoin.DataEncoders;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets
{
	/// <summary>
	/// FileSystemBlockRepository is a blocks repository that keeps the blocks in the file system.
	/// </summary>
	public class FileSystemBlockRepository : IRepository<uint256, Block>
	{
		public FileSystemBlockRepository(string blocksFolderPath, Network network)
		{
			BlocksFolderPath = blocksFolderPath;
			Network = network;
			CreateFolders();
			EnsureBackwardsCompatibility();
		}

		public string BlocksFolderPath { get; }
		public Network Network { get; }
		private AsyncLock BlockFolderLock { get; } = new AsyncLock();

		private void EnsureBackwardsCompatibility()
		{
			try
			{
				// Before Wasabi 1.1.13
				var wrongBlockFolderPath = Path.Combine(EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client")), $"Blocks{Network}");
				if (Directory.Exists(wrongBlockFolderPath))
				{
					using (BenchmarkLogger.Measure())
					{
						var cntSuccess = 0;
						var cntRedundant = 0;
						var cntFailure = 0;
						foreach (var filePath in Directory.EnumerateFiles(wrongBlockFolderPath))
						{
							try
							{
								var fileName = Path.GetFileName(filePath);
								var migrationPath = Path.Combine(BlocksFolderPath, fileName);

								// Unintuitively File.Move overwrite: false throws an IOException if the file already exists.
								// https://docs.microsoft.com/en-us/dotnet/api/system.io.file.move?view=netcore-3.1
								if (!File.Exists(migrationPath))
								{
									File.Move(filePath, migrationPath, overwrite: false);
									cntSuccess++;
								}
								else
								{
									cntRedundant++;
								}
							}
							catch (Exception ex)
							{
								Logger.LogDebug(ex);
								cntFailure++;
							}
						}

						Directory.Delete(wrongBlockFolderPath, recursive: true);

						if (cntSuccess > 0)
						{
							Logger.LogInfo($"Successfully migrated {cntSuccess} blocks to {BlocksFolderPath}.");
						}
						if (cntRedundant > 0)
						{
							Logger.LogInfo($"{cntRedundant} blocks were already in {BlocksFolderPath}.");
						}
						if (cntFailure > 0)
						{
							Logger.LogDebug($"Failed to migrate {cntFailure} blocks to {BlocksFolderPath}.");
						}
						Logger.LogInfo($"Deleted {wrongBlockFolderPath}.");
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Backwards compatibility could not be ensured.");
				Logger.LogWarning(ex);
			}
		}

		/// <summary>
		/// Gets a bitcoin block from the file system.
		/// </summary>
		/// <param name="hash">The block's hash that identifies the requested block.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The requested bitcoin block.</returns>
		public async Task<Block> GetAsync(uint256 hash, CancellationToken cancellationToken)
		{
			// Try get the block
			Block block = null;
			using (await BlockFolderLock.LockAsync().ConfigureAwait(false))
			{
				var encoder = new HexEncoder();
				var filePath = Path.Combine(BlocksFolderPath, hash.ToString());
				if (File.Exists(filePath))
				{
					try
					{
						var blockBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
						block = Block.Load(blockBytes, Network);
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
				using (await BlockFolderLock.LockAsync().ConfigureAwait(false))
				{
					if (!File.Exists(path))
					{
						await File.WriteAllBytesAsync(path, block.ToBytes()).ConfigureAwait(false);
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
				using (await BlockFolderLock.LockAsync().ConfigureAwait(false))
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
			using (await BlockFolderLock.LockAsync().ConfigureAwait(false))
			{
				return Directory.EnumerateFiles(BlocksFolderPath).Count();
			}
		}

		private void CreateFolders()
		{
			if (Directory.Exists(BlocksFolderPath))
			{
				if (Network == Network.RegTest)
				{
					Directory.Delete(BlocksFolderPath, true);
					Directory.CreateDirectory(BlocksFolderPath);
				}
			}
			else
			{
				Directory.CreateDirectory(BlocksFolderPath);
			}
		}
	}
}
