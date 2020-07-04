using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Microservices
{
	public class ProcessBridge : IProcessBridge
	{
		public ProcessBridge(string processPath)
		{
			ProcessPath = Guard.NotNullOrEmptyOrWhitespace(nameof(processPath), processPath);

			if (!File.Exists(ProcessPath))
			{
				throw new FileNotFoundException($"{Path.GetFileNameWithoutExtension(ProcessPath)} is not found.", ProcessPath);
			}
		}

		public string ProcessPath { get; }

		public Process Start(string arguments, bool openConsole)
		{
			var process = CreateProcessInstance(arguments, openConsole);
			try
			{
				process.Start();
			}
			catch
			{
				Logger.LogInfo($"{nameof(process.StartInfo.FileName)}: {process.StartInfo.FileName}");
				Logger.LogInfo($"{nameof(process.StartInfo.Arguments)}: {process.StartInfo.Arguments}");
				Logger.LogInfo($"{nameof(process.StartInfo.RedirectStandardOutput)}: {process.StartInfo.RedirectStandardOutput}");
				Logger.LogInfo($"{nameof(process.StartInfo.UseShellExecute)}: {process.StartInfo.UseShellExecute}");
				Logger.LogInfo($"{nameof(process.StartInfo.CreateNoWindow)}: {process.StartInfo.CreateNoWindow}");
				Logger.LogInfo($"{nameof(process.StartInfo.WindowStyle)}: {process.StartInfo.WindowStyle}");
				throw;
			}
			return process;
		}

		public async Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			// "using" makes sure the process exits at the end of this method.
			using var process = new ProcessAsync(CreateProcessInstance(arguments, openConsole));

			process.Start();

			await process.WaitForExitAsync(cancel);

			string responseString = openConsole ? string.Empty : await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
			return (responseString, exitCode: process.ExitCode);
		}

		protected Process CreateProcessInstance(string arguments, bool openConsole)
		{
			ProcessWindowStyle windowStyle;
			if (openConsole)
			{
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					throw new PlatformNotSupportedException($"{RuntimeInformation.OSDescription} is not supported.");
				}

				windowStyle = ProcessWindowStyle.Normal;
			}
			else
			{
				windowStyle = ProcessWindowStyle.Hidden;
			}

			var p = new Process();
			p.StartInfo.FileName = ProcessPath;
			p.StartInfo.Arguments = arguments;
			p.StartInfo.RedirectStandardOutput = !openConsole;
			p.StartInfo.UseShellExecute = openConsole;
			p.StartInfo.CreateNoWindow = !openConsole;
			p.StartInfo.WindowStyle = windowStyle;

			return p;
		}
	}
}
