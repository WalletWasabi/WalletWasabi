using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Packager
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			string packagerProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\"));
			string solutionDirectory = Path.GetFullPath(Path.Combine(packagerProjectDirectory, "..\\"));
			string guiProjectDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "WalletWasabi.Gui\\"));
			string binDistDirectory = Path.GetFullPath(Path.Combine(guiProjectDirectory, "bin\\dist"));
			Console.WriteLine($"{nameof(solutionDirectory)}:\t\t{solutionDirectory}");
			Console.WriteLine($"{nameof(packagerProjectDirectory)}:\t{packagerProjectDirectory}");
			Console.WriteLine($"{nameof(guiProjectDirectory)}:\t\t{guiProjectDirectory}");
			Console.WriteLine($"{nameof(binDistDirectory)}:\t\t{binDistDirectory}");

			string version = Helpers.Constants.ClientVersion.ToString();
			Console.WriteLine();
			Console.WriteLine($"{nameof(version)}:\t\t\t{version}");

			// https://docs.microsoft.com/en-us/dotnet/articles/core/rid-catalog
			// BOTTLENECKS:
			// Tor - win-32, linux-32, osx-64
			// .NET Core - win-32, linux-64, osx-64
			// Avalonia - win7-32, linux-64, osx-64
			// We'll only support x64, if someone complains, we can come back to it.
			var targets = new List<string>
			{
				"win7-x64",
				"linux-x64",
				"osx-x64"
			};
			Console.WriteLine();
			Console.Write($"{nameof(targets)}:\t\t\t");
			targets.ForEach(x =>
			{
				if (targets.Last() != x)
				{
					Console.Write($"{x}, ");
				}
				else
				{
					Console.Write(x);
				}
			});
			Console.WriteLine();

			var psiBuild = new ProcessStartInfo
			{
				FileName = "cmd",
				RedirectStandardInput = true,
				WorkingDirectory = guiProjectDirectory
			};
			var pBuild = Process.Start(psiBuild);
			pBuild.StandardInput.WriteLine("dotnet restore && dotnet build && exit");
			pBuild.WaitForExit();

			foreach (string target in targets)
			{
				string currentBinDistDirectory = Path.GetFullPath(Path.Combine(binDistDirectory, target));
				Console.WriteLine();
				Console.WriteLine($"{nameof(currentBinDistDirectory)}:\t{currentBinDistDirectory}");

				Console.WriteLine();
				if (Directory.Exists(currentBinDistDirectory))
				{
					IoHelpers.DeleteRecursivelyWithMagicDustAsync(currentBinDistDirectory).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {currentBinDistDirectory}");
				}
				if (!Directory.Exists(currentBinDistDirectory))
				{
					Directory.CreateDirectory(currentBinDistDirectory);
					Console.WriteLine($"Created {currentBinDistDirectory}");
				}

				var psiPublish = new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = $"publish --configuration Release --runtime {target} --output bin/dist/{target}",
					WorkingDirectory = guiProjectDirectory
				};
				var pPublish = Process.Start(psiPublish);
				pPublish.WaitForExit();
			}

			Console.WriteLine();
			Console.WriteLine("FINISHED! Press key to exit...");
			Console.ReadKey();
		}
	}
}
