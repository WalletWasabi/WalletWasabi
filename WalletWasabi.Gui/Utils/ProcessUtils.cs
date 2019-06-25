using Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WalletWasabi.Gui
{
	public enum ShellType
	{
		Generic,
		Windows,
		Unix
	}

	public struct ShellExecuteResult
	{
		public int ExitCode { get; set; }
		public string Output { get; set; }
		public string ErrorOutput { get; set; }
	}

	public static class ShellUtils
	{
		private static ShellType ExecutorType = ShellType.Generic;

		static ShellUtils()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
				|| RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				ExecutorType = ShellType.Unix;
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				ExecutorType = ShellType.Windows;
			}
		}

		public static ShellExecuteResult ExecuteShellCommand(string commandName, string args)
		{
			var outputBuilder = new StringBuilder();
			var errorBuilder = new StringBuilder();

			var exitCode = ExecuteShellCommand(commandName, args,
			(s, e) =>
			{
				outputBuilder.AppendLine(e.Data);
			},
			(s, e) =>
			{
				errorBuilder = new StringBuilder();
			},
			false, "");

			return new ShellExecuteResult() {
				ExitCode = exitCode,
				Output = outputBuilder.ToString().Trim(),
				ErrorOutput = errorBuilder.ToString().Trim()
			};
		}

		public static int ExecuteShellCommand(string commandName, string args, Action<object, DataReceivedEventArgs>
			outputReceivedCallback, Action<object, DataReceivedEventArgs> errorReceivedCallback = null, bool resolveExecutable = true,
			string workingDirectory = "", bool executeInShell = true, bool includeSystemPaths = true, params string[] extraPaths)
		{
			using (var shellProc = new Process {
				StartInfo = new ProcessStartInfo {
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					WorkingDirectory = workingDirectory
				}
			})

			{
				if (!includeSystemPaths)
				{
					shellProc.StartInfo.Environment["PATH"] = "";
				}
				foreach (var extraPath in extraPaths)
				{
					if (extraPath != null)
					{
						shellProc.StartInfo.Environment["PATH"] += $"{Path.DirectorySeparatorChar}{extraPath}";
					}
				}

				if (executeInShell)
				{
					if (ExecutorType == ShellType.Windows)
					{
						shellProc.StartInfo.FileName = ResolveFullExecutablePath("cmd.exe");
						shellProc.StartInfo.Arguments = $"/C {(resolveExecutable ? ResolveFullExecutablePath(commandName, true, extraPaths) : commandName)} {args}";
						shellProc.StartInfo.CreateNoWindow = true;
					}
					else //Unix
					{
						shellProc.StartInfo.FileName = "sh";
						shellProc.StartInfo.Arguments = $"-c \"{(resolveExecutable ? ResolveFullExecutablePath(commandName) : commandName)} {args}\"";
						shellProc.StartInfo.CreateNoWindow = true;
					}
				}
				else
				{
					shellProc.StartInfo.FileName = (resolveExecutable ? ResolveFullExecutablePath(commandName, true, extraPaths) : commandName);
					shellProc.StartInfo.Arguments = args;
					shellProc.StartInfo.CreateNoWindow = true;
				}

				shellProc.OutputDataReceived += (s, a) => outputReceivedCallback(s, a);

				if (errorReceivedCallback != null)
				{
					shellProc.ErrorDataReceived += (s, a) => errorReceivedCallback(s, a);
				}

				shellProc.Start();

				shellProc.BeginOutputReadLine();
				shellProc.BeginErrorReadLine();

				shellProc.WaitForExit();

				return shellProc.ExitCode;
			}
		}

		/// <summary>
		/// Attempts to locate the full path to a script
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static string ResolveFullExecutablePath(string fileName, bool returnNullOnFailure = true, params string[] extraPaths)
		{
			if (File.Exists(fileName))
				return Path.GetFullPath(fileName);

			if (ExecutorType == ShellType.Windows)
			{
				var values = new List<string>(extraPaths);
				values.AddRange(new List<string>(Environment.GetEnvironmentVariable("PATH").Split(';')));

				foreach (var path in values)
				{
					var fullPath = Path.Combine(path, fileName);
					if (File.Exists(fullPath))
						return fullPath;
				}
			}
			else
			{
				//Use the which command
				var outputBuilder = new StringBuilder();
				ExecuteShellCommand("which", $"\"{fileName}\"", (s, e) =>
				{
					outputBuilder.AppendLine(e.Data);
				}, (s, e) => { }, false);
				var procOutput = outputBuilder.ToString();
				if (string.IsNullOrWhiteSpace(procOutput))
				{
					return returnNullOnFailure ? null : fileName;
				}
				return procOutput.Trim();
			}
			return returnNullOnFailure ? null : fileName;
		}
	}
}