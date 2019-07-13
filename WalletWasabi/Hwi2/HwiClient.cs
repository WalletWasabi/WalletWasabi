using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Hwi2
{
	public class HwiClient
	{
		#region PropertiesAndMembers

		public Network Network { get; }

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public HwiClient(Network network)
		{
			Network = Guard.NotNull(nameof(network), network);
		}

		#endregion ConstructorsAndInitializers

		#region Commands

		public async Task<string> SendCommandAsync(string arguments, CancellationToken cancel)
		{
			var fullBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
			var hwiPath = Path.Combine(fullBaseDirectory, "Hwi2", "Binaries", "hwi-win64", "hwi.exe");

			try
			{
				using (var process = Process.Start(
					new ProcessStartInfo
					{
						FileName = hwiPath,
						Arguments = arguments,
						RedirectStandardOutput = true,
						UseShellExecute = false,
						CreateNoWindow = true,
						WindowStyle = ProcessWindowStyle.Hidden
					}
				))
				{
					await process.WaitForExitAsync(cancel).ConfigureAwait(false);

					if (process.ExitCode != 0)
					{
						throw new IOException($"'hwi {arguments}' exited with incorrect exit code.", process.ExitCode);
					}

					string responseString = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

					return responseString;
				}
			}
			catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
			{
				throw new OperationCanceledException($"'hwi {arguments}' operation is canceled.", ex);
			}
		}

		public async Task<Version> GetVersionAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync("--version", cancel).ConfigureAwait(false);

			// Example output: hwi 1.0.0
			if (TryParseVersion(responseString, "hwi", out Version v1))
			{
				return v1;
			}

			// Example output: hwi.exe 1.0.0
			if (TryParseVersion(responseString, "hwi.exe", out Version v2))
			{
				return v2;
			}

			throw new FormatException($"Cannot parse version from HWI's response. Response: {responseString}.");
		}

		public async Task<string> GetHelpAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync("--help", cancel).ConfigureAwait(false);

			return responseString;
		}

		#endregion Commands

		#region Helpers

		public static bool TryParseVersion(string hwiResponse, string substringFrom, out Version version)
		{
			int startIndex = hwiResponse.IndexOf(substringFrom) + substringFrom.Length;
			var versionString = hwiResponse.Substring(startIndex).Trim();
			version = null;
			if (Version.TryParse(versionString, out Version v))
			{
				version = v;
				return true;
			}

			return false;
		}

		#endregion Helpers
	}
}
