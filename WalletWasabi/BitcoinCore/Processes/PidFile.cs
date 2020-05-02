using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Configuration;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Processes
{
	public class PidFile
	{
		public const string FileName = "bitcoin.pid";

		public PidFile(string dataDir, Network network)
		{
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			Network = Guard.NotNull(nameof(network), network);
			FilePath = Path.Combine(DataDir, NetworkTranslator.GetDataDirPrefix(Network), FileName);
		}

		public string DataDir { get; }
		public Network Network { get; }
		public string FilePath { get; }

		public bool Exists => File.Exists(FilePath);

		public async Task<int?> TryReadAsync()
		{
			if (!Exists)
			{
				return null;
			}

			var pidString = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
			return int.Parse(pidString);
		}

		public async Task SerializeAsync(int pid)
		{
			IoHelpers.EnsureContainingDirectoryExists(FilePath);
			await File.WriteAllTextAsync(FilePath, pid.ToString()).ConfigureAwait(false);
		}

		public void TryDelete()
		{
			try
			{
				if (File.Exists(FilePath))
				{
					File.Delete(FilePath);
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}
	}
}
