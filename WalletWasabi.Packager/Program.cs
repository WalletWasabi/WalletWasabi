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
			pBuild.StandardInput.WriteLine("dotnet clean && dotnet restore && dotnet build && exit");
			pBuild.WaitForExit();

			Console.WriteLine();
			if (Directory.Exists(binDistDirectory))
			{
				IoHelpers.DeleteRecursivelyWithMagicDustAsync(binDistDirectory).GetAwaiter().GetResult();
				Console.WriteLine($"Deleted {binDistDirectory}");
			}

			foreach (string target in targets)
			{
				string currentBinDistDirectory = Path.GetFullPath(Path.Combine(binDistDirectory, target));
				Console.WriteLine();
				Console.WriteLine($"{nameof(currentBinDistDirectory)}:\t{currentBinDistDirectory}");

				Console.WriteLine();
				if (!Directory.Exists(currentBinDistDirectory))
				{
					Directory.CreateDirectory(currentBinDistDirectory);
					Console.WriteLine($"Created {currentBinDistDirectory}");
				}

				// https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-publish?tabs=netcore21
				// -c|--configuration {Debug|Release}
				//		Defines the build configuration. The default value is Debug.
				// --force
				//		Forces all dependencies to be resolved even if the last restore was successful. Specifying this flag is the same as deleting the project.assets.json file.
				// -o|--output <OUTPUT_DIRECTORY>
				//		Specifies the path for the output directory.
				//		If not specified, it defaults to ./bin/[configuration]/[framework]/publish/ for a framework-dependent deployment or
				//		./bin/[configuration]/[framework]/[runtime]/publish/ for a self-contained deployment.
				//		If the path is relative, the output directory generated is relative to the project file location, not to the current working directory.
				// --self-contained
				//		Publishes the .NET Core runtime with your application so the runtime doesn't need to be installed on the target machine.
				//		If a runtime identifier is specified, its default value is true. For more information about the different deployment types, see .NET Core application deployment.
				// -r|--runtime <RUNTIME_IDENTIFIER>
				//		Publishes the application for a given runtime. This is used when creating a self-contained deployment (SCD).
				//		For a list of Runtime Identifiers (RIDs), see the RID catalog. Default is to publish a framework-dependent deployment (FDD).
				var psiPublish = new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = $"publish --configuration Release --force --output bin/dist/{target} --self-contained true --runtime {target}",
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
