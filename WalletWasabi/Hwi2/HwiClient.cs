using NBitcoin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi2.Models;

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

		private async Task<string> SendCommandAsync(IEnumerable<HwiOptions> options, HwiCommands? command, CancellationToken cancel)
		{
			var fullBaseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
			var hwiPath = Path.Combine(fullBaseDirectory, "Hwi2", "Binaries", "hwi-win64", "hwi.exe");

			options = options ?? Enumerable.Empty<HwiOptions>();
			var fullOptions = new List<HwiOptions>(options);

			if (Network != Network.Main)
			{
				fullOptions.Add(HwiOptions.TestNet);
			}

			var optionsString = string.Join(" --", fullOptions.Select(x => x.ToString().ToLowerInvariant()));
			optionsString = string.IsNullOrWhiteSpace(optionsString) ? "" : $"--{optionsString}";
			var argumentBuilder = new StringBuilder(optionsString);
			if (command != null)
			{
				if (argumentBuilder.Length != 0)
				{
					argumentBuilder.Append(' ');
				}
				argumentBuilder.Append(command.ToString().ToLowerInvariant());
			}

			var arguments = argumentBuilder.ToString().Trim();

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
			string responseString = await SendCommandAsync(options: new[] { HwiOptions.Version }, command: null, cancel).ConfigureAwait(false);

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
			string responseString = await SendCommandAsync(options: new[] { HwiOptions.Help }, command: null, cancel).ConfigureAwait(false);

			return responseString;
		}

		public async Task<IEnumerable<string>> EnumerateAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: null, command: HwiCommands.Enumerate, cancel).ConfigureAwait(false);
			var jarr = JArray.Parse(responseString);

			var hwis = new List<string>();
			foreach (JObject json in jarr)
			{
				string jsonString = json.ToString();
				hwis.Add(jsonString);
			}

			return hwis;
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
