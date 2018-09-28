using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Packager
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			// 0. Dump Client version (or else wrong .msi will be created) - Helpers.Constants.ClientVersion
			// 1. Publish with Packager.
			// 2. Build WIX project with Release and x64 configuration.
			// 3. Sign with Packager.
			bool doPublish = true;
			bool doSign = false;

			string packagerProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\"));
			string solutionDirectory = Path.GetFullPath(Path.Combine(packagerProjectDirectory, "..\\"));
			string guiProjectDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "WalletWasabi.Gui\\"));
			string wixProjectDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "WalletWasabi.WindowsInstaller\\"));
			string binDistDirectory = Path.GetFullPath(Path.Combine(guiProjectDirectory, "bin\\dist"));
			Console.WriteLine($"{nameof(solutionDirectory)}:\t\t{solutionDirectory}");
			Console.WriteLine($"{nameof(packagerProjectDirectory)}:\t{packagerProjectDirectory}");
			Console.WriteLine($"{nameof(guiProjectDirectory)}:\t\t{guiProjectDirectory}");
			Console.WriteLine($"{nameof(wixProjectDirectory)}:\t\t{wixProjectDirectory}");
			Console.WriteLine($"{nameof(binDistDirectory)}:\t\t{binDistDirectory}");

			string versionPrefix = Helpers.Constants.ClientVersion.ToString();
			string executableName = "wassabee";
			Console.WriteLine();
			Console.WriteLine($"{nameof(versionPrefix)}:\t\t\t{versionPrefix}");
			Console.WriteLine($"{nameof(executableName)}:\t\t\t{executableName}");

			// https://docs.microsoft.com/en-us/dotnet/articles/core/rid-catalog
			// BOTTLENECKS:
			// Tor - win-32, linux-32, osx-64
			// .NET Core - win-32, linux-64, osx-64
			// Avalonia - win7-32, linux-64, osx-64
			// We'll only support x64, if someone complains, we can come back to it.
			// For 32 bit Windows there needs to be a lot of WIX configuration to be done.
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

			if (doPublish)
			{
				if (Directory.Exists(binDistDirectory))
				{
					IoHelpers.DeleteRecursivelyWithMagicDustAsync(binDistDirectory).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {binDistDirectory}");
				}

				var psiBuild = new ProcessStartInfo
				{
					FileName = "cmd",
					RedirectStandardInput = true,
					WorkingDirectory = guiProjectDirectory
				};
				var pBuild = Process.Start(psiBuild);
				pBuild.StandardInput.WriteLine("dotnet clean --configuration Release && exit");
				pBuild.WaitForExit();

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
					// --version-suffix <VERSION_SUFFIX>
					//		Defines the version suffix to replace the asterisk (*) in the version field of the project file.
					// https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-restore?tabs=netcore2x
					// --disable-parallel
					//		Disables restoring multiple projects in parallel.
					// --no-cache
					//		Specifies to not cache packages and HTTP requests.
					// https://github.com/dotnet/docs/issues/7568
					// /p:Version=1.2.3.4
					//		"dotnet publish" supports msbuild command line options like /p:Version=1.2.3.4
					var psiPublish = new ProcessStartInfo
					{
						FileName = "dotnet",
						Arguments = $"publish --configuration Release --force --output {currentBinDistDirectory} --self-contained true --runtime {target} /p:VersionPrefix={versionPrefix} --disable-parallel --no-cache",
						WorkingDirectory = guiProjectDirectory
					};
					var pPublish = Process.Start(psiPublish);
					pPublish.WaitForExit();

					// Rename the final exe.
					string oldExecutablePath;
					string newExecutablePath;
					if (target.StartsWith("win"))
					{
						oldExecutablePath = Path.Combine(currentBinDistDirectory, "WalletWasabi.Gui.exe");
						newExecutablePath = Path.Combine(currentBinDistDirectory, $"{executableName}.exe");
					}
					else // Linux & OSX
					{
						oldExecutablePath = Path.Combine(currentBinDistDirectory, "WalletWasabi.Gui");
						newExecutablePath = Path.Combine(currentBinDistDirectory, executableName);
					}
					File.Move(oldExecutablePath, newExecutablePath);

					if (target.StartsWith("win"))
					{
						var psiEditbin = new ProcessStartInfo
						{
							FileName = "editbin",
							Arguments = $"\"{newExecutablePath}\" /SUBSYSTEM:WINDOWS",
							WorkingDirectory = currentBinDistDirectory
						};
						var pEditbin = Process.Start(psiEditbin);
						pEditbin.WaitForExit();

						var icoPath = Path.Combine(guiProjectDirectory, "Assets", "WasabiLogo.ico");
						var psiRcedit = new ProcessStartInfo
						{
							FileName = "rcedit",
							Arguments = $"\"{newExecutablePath}\" --set-icon \"{icoPath}\" --set-file-version \"{versionPrefix}\" --set-product-version \"{versionPrefix}\" --set-version-string \"LegalCopyright\" \"MIT\" --set-version-string \"CompanyName\" \"zkSNACKs\" --set-version-string \"FileDescription\" \"Privacy focused, ZeroLink compliant Bitcoin wallet.\" --set-version-string \"ProductName\" \"Wasabi Wallet\"",
							WorkingDirectory = currentBinDistDirectory
						};
						var pRcedit = Process.Start(psiRcedit);
						pRcedit.WaitForExit();
					}
				}
			}

			if (doSign is true)
			{
				foreach (string target in targets)
				{
					var publishedFolder = Path.Combine(binDistDirectory, $"{target}");

					if (target.StartsWith("win", StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine("Move created .msi");
						var msiPath = Path.Combine(wixProjectDirectory, @"bin\Release\WasabiInstaller.msi");
						if (!File.Exists(msiPath))
						{
							throw new Exception(".msi doesn't exist. Expected path: WasabiInstaller.msi.");
						}
						var msiFileName = Path.GetFileName(msiPath);
						var newMsiPath = Path.Combine(binDistDirectory, msiFileName);
						File.Move(msiPath, newMsiPath);
					}
					else if (target.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine("Create Linux .tar.gz");
						if (!Directory.Exists(publishedFolder))
						{
							throw new Exception($"{publishedFolder} doesn't exist.");
						}
						var newFolderName = "WasabiLinux";
						var newFolderPath = Path.Combine(binDistDirectory, newFolderName);
						Directory.Move(publishedFolder, newFolderPath);
						publishedFolder = newFolderPath;

						var psiTar = new ProcessStartInfo
						{
							FileName = "cmd",
							RedirectStandardInput = true,
							WorkingDirectory = binDistDirectory
						};
						var tarProcess = Process.Start(psiTar);
						tarProcess.StandardInput.WriteLine($"wsl tar -pczvf {newFolderName}.tar.gz {newFolderName} && exit");
						tarProcess.WaitForExit();
					}
					else // if (target.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine("Create OSX .tar.gz");
						if (!Directory.Exists(publishedFolder))
						{
							throw new Exception($"{publishedFolder} doesn't exist.");
						}
						var newFolderName = "WasabiOsx";
						var newFolderPath = Path.Combine(binDistDirectory, newFolderName);
						Directory.Move(publishedFolder, newFolderPath);
						publishedFolder = newFolderPath;

						var psiTar = new ProcessStartInfo
						{
							FileName = "cmd",
							RedirectStandardInput = true,
							WorkingDirectory = binDistDirectory
						};
						var tarProcess = Process.Start(psiTar);
						tarProcess.StandardInput.WriteLine($"wsl tar -pczvf {newFolderName}.tar.gz {newFolderName} && exit");
						tarProcess.WaitForExit();
					}

					if (Directory.Exists(publishedFolder))
					{
						IoHelpers.DeleteRecursivelyWithMagicDustAsync(publishedFolder).GetAwaiter().GetResult();
						Console.WriteLine($"Deleted {publishedFolder}");
					}
				}

				Console.WriteLine("Signing final files...");
				var finalFiles = Directory.GetFiles(binDistDirectory);

				foreach (var finalFile in finalFiles)
				{
					var psiSignProcess = new ProcessStartInfo
					{
						FileName = "cmd",
						RedirectStandardInput = true,
						WorkingDirectory = binDistDirectory
					};
					var signProcess = Process.Start(psiSignProcess);
					signProcess.StandardInput.WriteLine($"gpg --armor --detach-sign {finalFile} && exit");
					signProcess.WaitForExit();

					var psiRestoreHeat = new ProcessStartInfo
					{
						FileName = "cmd",
						RedirectStandardInput = true,
						WorkingDirectory = wixProjectDirectory
					};
					var restoreHeatProcess = Process.Start(psiRestoreHeat);
					restoreHeatProcess.StandardInput.WriteLine($"git checkout -- ComponentsGenerated.wxs && exit");
					restoreHeatProcess.WaitForExit();
				}

				IoHelpers.OpenFolderInFileExplorer(binDistDirectory);
			}

			Console.WriteLine();
			Console.WriteLine("FINISHED! Press key to exit...");
			Console.ReadKey();
		}
	}
}
