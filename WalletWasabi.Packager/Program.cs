using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Packager
{
	public static class Program
	{
#pragma warning disable CS0162 // Unreachable code detected
		// 0. Dump Client version (or else wrong .msi will be created) - Helpers.Constants.ClientVersion
		// 1. Publish with Packager.
		// 2. Build WIX project with Release and x64 configuration.
		// 3. Sign with Packager, set restore true so the password won't be kept.

		public const bool DoPublish = true;
		public const bool DoSign = false;
		public const bool DoRestoreProgramCs = false;

		public const string PfxPath = "C:\\digicert.pfx";
		public const string ExecutableName = Constants.ExecutableName;

		// https://docs.microsoft.com/en-us/dotnet/articles/core/rid-catalog
		// BOTTLENECKS:
		// Tor - win-32, linux-32, osx-64
		// .NET Core - win-32, linux-64, osx-64
		// Avalonia - win7-32, linux-64, osx-64
		// We'll only support x64, if someone complains, we can come back to it.
		// For 32 bit Windows there needs to be a lot of WIX configuration to be done.
		private static string[] Targets = new[]
		{
			"win7-x64",
			"linux-x64",
			"osx-x64"
		};

		private static string VersionPrefix = Constants.ClientVersion.Revision == 0 ? Constants.ClientVersion.ToString(3) : Constants.ClientVersion.ToString();

		private static bool OnlyBinaries;
		private static bool IsContinuousDelivery;

		public static string PackagerProjectDirectory { get; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
		public static string SolutionDirectory { get; } = Path.GetFullPath(Path.Combine(PackagerProjectDirectory, ".."));
		public static string DesktopProjectDirectory { get; } = Path.GetFullPath(Path.Combine(SolutionDirectory, "WalletWasabi.Fluent.Desktop"));
		public static string LibraryProjectDirectory { get; } = Path.GetFullPath(Path.Combine(SolutionDirectory, "WalletWasabi"));
		public static string WixProjectDirectory { get; } = Path.GetFullPath(Path.Combine(SolutionDirectory, "WalletWasabi.WindowsInstaller"));
		public static string BinDistDirectory { get; } = Path.GetFullPath(Path.Combine(DesktopProjectDirectory, "bin", "dist"));

		/// <summary>
		/// Main entry point.
		/// </summary>
		private static async Task Main(string[] args)
		{
			var argsProcessor = new ArgsProcessor(args);

			// For now this is enough. If you run it on macOS you want to sign.
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				MacSignTools.Sign();
				return;
			}

			// Only binaries mode is for deterministic builds.
			OnlyBinaries = argsProcessor.IsOnlyBinariesMode();

			IsContinuousDelivery = argsProcessor.IsContinuousDeliveryMode();

			ReportStatus();

			if (DoPublish || OnlyBinaries)
			{
				Publish();

				IoHelpers.OpenFolderInFileExplorer(BinDistDirectory);
			}

			if (!OnlyBinaries)
			{
				if (DoSign)
				{
					Sign();
				}

				if (DoRestoreProgramCs)
				{
					RestoreProgramCs();
				}
			}
		}

		private static void ReportStatus()
		{
			if (OnlyBinaries)
			{
				Console.WriteLine($"I'll only generate binaries and disregard all other options.");
			}
			Console.WriteLine($"{nameof(VersionPrefix)}:\t\t\t{VersionPrefix}");
			Console.WriteLine($"{nameof(ExecutableName)}:\t\t\t{ExecutableName}");

			Console.WriteLine();
			Console.Write($"{nameof(Targets)}:\t\t\t");
			foreach (var target in Targets)
			{
				if (Targets.Last() != target)
				{
					Console.Write($"{target}, ");
				}
				else
				{
					Console.Write(target);
				}
			}
			Console.WriteLine();
		}

		private static void RestoreProgramCs()
		{
			StartProcessAndWaitForExit("cmd", PackagerProjectDirectory, "git checkout -- Program.cs && exit");
		}

		private static void Sign()
		{
			foreach (string target in Targets)
			{
				if (target.StartsWith("win", StringComparison.OrdinalIgnoreCase))
				{
					string publishedFolder = Path.Combine(BinDistDirectory, target);

					Console.WriteLine("Move created .msi");
					var msiPath = Path.Combine(WixProjectDirectory, "bin", "Release", "Wasabi.msi");
					if (!File.Exists(msiPath))
					{
						throw new Exception(".msi does not exist. Expected path: Wasabi.msi.");
					}
					var msiFileName = Path.GetFileNameWithoutExtension(msiPath);
					var newMsiPath = Path.Combine(BinDistDirectory, $"{msiFileName}-{VersionPrefix}.msi");
					File.Copy(msiPath, newMsiPath);

					Console.Write("Enter Code Signing Certificate Password: ");
					string pfxPassword = PasswordConsole.ReadPassword();

					// Sign code with digicert.
					StartProcessAndWaitForExit("cmd", BinDistDirectory, $"signtool sign /d \"Wasabi Wallet\" /f \"{PfxPath}\" /p {pfxPassword} /t http://timestamp.digicert.com /a \"{newMsiPath}\" && exit");

					IoHelpers.TryDeleteDirectoryAsync(publishedFolder).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {publishedFolder}");
				}
				else if (target.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
				{
					string dmgFilePath = Path.Combine(BinDistDirectory, $"Wasabi-{VersionPrefix}.dmg");
					if (!File.Exists(dmgFilePath))
					{
						throw new Exception(".dmg does not exist.");
					}
					string zipFilePath = Path.Combine(BinDistDirectory, $"Wasabi-osx-{VersionPrefix}.zip");
					if (File.Exists(zipFilePath))
					{
						File.Delete(zipFilePath);
					}
				}
			}

			Console.WriteLine("Signing final files...");
			var finalFiles = Directory.GetFiles(BinDistDirectory);

			foreach (var finalFile in finalFiles)
			{
				StartProcessAndWaitForExit("cmd", BinDistDirectory, $"gpg --armor --detach-sign {finalFile} && exit");

				StartProcessAndWaitForExit("cmd", WixProjectDirectory, $"git checkout -- ComponentsGenerated.wxs && exit");
			}

			IoHelpers.OpenFolderInFileExplorer(BinDistDirectory);
		}

		private static void Publish()
		{
			if (Directory.Exists(BinDistDirectory))
			{
				IoHelpers.TryDeleteDirectoryAsync(BinDistDirectory).GetAwaiter().GetResult();
				Console.WriteLine($"Deleted {BinDistDirectory}");
			}

			StartProcessAndWaitForExit("cmd", DesktopProjectDirectory, "dotnet clean --configuration Release && exit");

			var desktopBinReleaseDirectory = Path.GetFullPath(Path.Combine(DesktopProjectDirectory, "bin", "Release"));
			var libraryBinReleaseDirectory = Path.GetFullPath(Path.Combine(LibraryProjectDirectory, "bin", "Release"));
			if (Directory.Exists(desktopBinReleaseDirectory))
			{
				IoHelpers.TryDeleteDirectoryAsync(desktopBinReleaseDirectory).GetAwaiter().GetResult();
				Console.WriteLine($"Deleted {desktopBinReleaseDirectory}");
			}
			if (Directory.Exists(libraryBinReleaseDirectory))
			{
				IoHelpers.TryDeleteDirectoryAsync(libraryBinReleaseDirectory).GetAwaiter().GetResult();
				Console.WriteLine($"Deleted {libraryBinReleaseDirectory}");
			}

			var deterministicFileNameTag = IsContinuousDelivery ? $"{DateTimeOffset.UtcNow:ddMMyyyy}{DateTimeOffset.UtcNow.TimeOfDay.TotalSeconds}" : VersionPrefix;
			var deliveryPath = IsContinuousDelivery ? Path.Combine(BinDistDirectory, "cdelivery") : BinDistDirectory;

			IoHelpers.EnsureDirectoryExists(deliveryPath);
			Console.WriteLine($"Binaries will be delivered here: {deliveryPath}");

			foreach (string target in Targets)
			{
				string publishedFolder = Path.Combine(BinDistDirectory, target);
				string currentBinDistDirectory = publishedFolder;

				Console.WriteLine();
				Console.WriteLine($"{nameof(currentBinDistDirectory)}:\t{currentBinDistDirectory}");

				Console.WriteLine();
				if (!Directory.Exists(currentBinDistDirectory))
				{
					Directory.CreateDirectory(currentBinDistDirectory);
					Console.WriteLine($"Created {currentBinDistDirectory}");
				}

				StartProcessAndWaitForExit("dotnet", DesktopProjectDirectory, arguments: "clean");

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
				//		Publishes the .NET Core runtime with your application so the runtime does not need to be installed on the target machine.
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

				string dotnetProcessArgs = string.Join(
					" ",
					$"publish",
					$"--configuration Release",
					$"--force",
					$"--output \"{currentBinDistDirectory}\"",
					$"--self-contained true",
					$"--runtime \"{target}\"",
					$"--disable-parallel",
					$"--no-cache",
					$"/p:VersionPrefix={VersionPrefix}",
					$"/p:DebugType=none",
					$"/p:DebugSymbols=false",
					$"/p:ErrorReport=none",
					$"/p:DocumentationFile=\"\"",
					$"/p:Deterministic=true",
					$"/p:RestoreLockedMode=true");

				StartProcessAndWaitForExit(
					"dotnet",
					DesktopProjectDirectory,
					arguments: dotnetProcessArgs,
					redirectStandardOutput: true);

				Tools.ClearSha512Tags(currentBinDistDirectory);

				// Remove Tor binaries that are not relevant to the platform.
				var toNotRemove = "";
				if (target.StartsWith("win"))
				{
					toNotRemove = "win";
				}
				else if (target.StartsWith("linux"))
				{
					toNotRemove = "lin";
				}
				else if (target.StartsWith("osx"))
				{
					toNotRemove = "osx";
				}

				// Remove binaries that are not relevant to the platform.
				var binaryFolder = new DirectoryInfo(Path.Combine(currentBinDistDirectory, "Microservices", "Binaries"));

				foreach (var dir in binaryFolder.EnumerateDirectories())
				{
					if (!dir.Name.Contains(toNotRemove, StringComparison.OrdinalIgnoreCase))
					{
						IoHelpers.TryDeleteDirectoryAsync(dir.FullName).GetAwaiter().GetResult();
					}
				}

				// Rename the final exe.
				string oldExecutablePath;
				string newExecutablePath;
				if (target.StartsWith("win"))
				{
					oldExecutablePath = Path.Combine(currentBinDistDirectory, "WalletWasabi.Fluent.Desktop.exe");
					newExecutablePath = Path.Combine(currentBinDistDirectory, $"{ExecutableName}.exe");

					// Delete unused executables.
					File.Delete(Path.Combine(currentBinDistDirectory, "WalletWasabi.Fluent.exe"));
				}
				else // Linux & OSX
				{
					oldExecutablePath = Path.Combine(currentBinDistDirectory, "WalletWasabi.Fluent.Desktop");
					newExecutablePath = Path.Combine(currentBinDistDirectory, ExecutableName);

					// Delete unused executables.
					File.Delete(Path.Combine(currentBinDistDirectory, "WalletWasabi.Fluent"));
				}
				File.Move(oldExecutablePath, newExecutablePath);

				long installedSizeKb = Tools.DirSize(new DirectoryInfo(publishedFolder)) / 1000;

				if (target.StartsWith("win"))
				{
					// IF IT'S IN ONLYBINARIES MODE DON'T DO ANYTHING FANCY PACKAGING AFTER THIS!!!
					if (OnlyBinaries)
					{
						continue; // In Windows build at this moment it does not matter though.
					}

					ZipFile.CreateFromDirectory(currentBinDistDirectory, Path.Combine(deliveryPath, $"Wasabi-{deterministicFileNameTag}-{target}.zip"));

					if (IsContinuousDelivery)
					{
						continue;
					}
				}
				else if (target.StartsWith("osx"))
				{
					// IF IT'S IN ONLYBINARIES MODE DON'T DO ANYTHING FANCY PACKAGING AFTER THIS!!!
					if (OnlyBinaries)
					{
						continue;
					}

					ZipFile.CreateFromDirectory(currentBinDistDirectory, Path.Combine(deliveryPath, $"Wasabi-{deterministicFileNameTag}-{target}.zip"));

					if (IsContinuousDelivery)
					{
						continue;
					}

					ZipFile.CreateFromDirectory(currentBinDistDirectory, Path.Combine(BinDistDirectory, $"Wasabi-osx-{VersionPrefix}.zip"));

					IoHelpers.TryDeleteDirectoryAsync(currentBinDistDirectory).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {currentBinDistDirectory}");
				}
				else if (target.StartsWith("linux"))
				{
					// IF IT'S IN ONLYBINARIES MODE DON'T DO ANYTHING FANCY PACKAGING AFTER THIS!!!
					if (OnlyBinaries)
					{
						continue;
					}

					ZipFile.CreateFromDirectory(currentBinDistDirectory, Path.Combine(deliveryPath, $"Wasabi-{deterministicFileNameTag}-{target}.zip"));

					if (IsContinuousDelivery)
					{
						continue;
					}

					Console.WriteLine("Create Linux .tar.gz");
					if (!Directory.Exists(publishedFolder))
					{
						throw new Exception($"{publishedFolder} does not exist.");
					}
					var newFolderName = $"Wasabi-{VersionPrefix}";
					var newFolderPath = Path.Combine(BinDistDirectory, newFolderName);
					Directory.Move(publishedFolder, newFolderPath);
					publishedFolder = newFolderPath;

					var driveLetterUpper = BinDistDirectory[0];
					var driveLetterLower = char.ToLower(driveLetterUpper);

					var linuxPath = $"/mnt/{driveLetterLower}/{Tools.LinuxPath(BinDistDirectory[3..])}";

					var chmodExecutablesArgs = "-type f \\( -name 'wassabee' -o -name 'hwi' -o -name 'bitcoind' -o -name 'tor' \\) -exec chmod u+x {} \\;";

					var commands = new[]
					{
						"cd ~",
						$"sudo umount /mnt/{driveLetterLower}",
						$"sudo mount -t drvfs {driveLetterUpper}: /mnt/{driveLetterLower} -o metadata",
						$"cd {linuxPath}",
						$"sudo find ./{newFolderName} -type f -exec chmod 644 {{}} \\;",
						$"sudo find ./{newFolderName} {chmodExecutablesArgs}",
						$"tar -pczvf {newFolderName}.tar.gz {newFolderName}"
					};
					string arguments = string.Join(" && ", commands);

					StartProcessAndWaitForExit("wsl", BinDistDirectory, arguments: arguments);

					Console.WriteLine("Create Linux .deb");

					var debFolderRelativePath = "deb";
					var debFolderPath = Path.Combine(BinDistDirectory, debFolderRelativePath);
					var linuxUsrLocalBinFolder = "/usr/local/bin/";
					var debUsrLocalBinFolderRelativePath = Path.Combine(debFolderRelativePath, "usr", "local", "bin");
					var debUsrLocalBinFolderPath = Path.Combine(BinDistDirectory, debUsrLocalBinFolderRelativePath);
					Directory.CreateDirectory(debUsrLocalBinFolderPath);
					var debUsrAppFolderRelativePath = Path.Combine(debFolderRelativePath, "usr", "share", "applications");
					var debUsrAppFolderPath = Path.Combine(BinDistDirectory, debUsrAppFolderRelativePath);
					Directory.CreateDirectory(debUsrAppFolderPath);
					var debUsrShareIconsFolderRelativePath = Path.Combine(debFolderRelativePath, "usr", "share", "icons", "hicolor");
					var debUsrShareIconsFolderPath = Path.Combine(BinDistDirectory, debUsrShareIconsFolderRelativePath);
					var debianFolderRelativePath = Path.Combine(debFolderRelativePath, "DEBIAN");
					var debianFolderPath = Path.Combine(BinDistDirectory, debianFolderRelativePath);
					Directory.CreateDirectory(debianFolderPath);
					newFolderName = "wasabiwallet";
					var linuxWasabiWalletFolder = Tools.LinuxPathCombine(linuxUsrLocalBinFolder, newFolderName);
					var newFolderRelativePath = Path.Combine(debUsrLocalBinFolderRelativePath, newFolderName);
					newFolderPath = Path.Combine(BinDistDirectory, newFolderRelativePath);
					Directory.Move(publishedFolder, newFolderPath);

					var assetsFolder = Path.Combine(DesktopProjectDirectory, "Assets");
					var assetsInfo = new DirectoryInfo(assetsFolder);

					foreach (var file in assetsInfo.EnumerateFiles())
					{
						var number = file.Name.Split(new string[] { "WasabiLogo", ".png" }, StringSplitOptions.RemoveEmptyEntries);
						if (number.Length == 1 && int.TryParse(number.First(), out int size))
						{
							string destFolder = Path.Combine(debUsrShareIconsFolderPath, $"{size}x{size}", "apps");
							Directory.CreateDirectory(destFolder);
							file.CopyTo(Path.Combine(destFolder, $"{ExecutableName}.png"));
						}
					}

					var controlFilePath = Path.Combine(debianFolderPath, "control");

					// License format does not yet work, but should work in the future, it's work in progress: https://bugs.launchpad.net/ubuntu/+source/software-center/+bug/435183
					var controlFileContent = $"Package: {ExecutableName}\n" +
						$"Priority: optional\n" +
						$"Section: utils\n" +
						$"Maintainer: nopara73 <adam.ficsor73@gmail.com>\n" +
						$"Version: {VersionPrefix}\n" +
						$"Homepage: http://wasabiwallet.io\n" +
						$"Vcs-Git: git://github.com/zkSNACKs/WalletWasabi.git\n" +
						$"Vcs-Browser: https://github.com/zkSNACKs/WalletWasabi\n" +
						$"Architecture: amd64\n" +
						$"License: Open Source (MIT)\n" +
						$"Installed-Size: {installedSizeKb}\n" +
						$"Description: open-source, non-custodial, privacy focused Bitcoin wallet\n" +
						$"  Built-in Tor, CoinJoin, PayJoin and Coin Control features.\n";

					File.WriteAllText(controlFilePath, controlFileContent, Encoding.ASCII);

					var desktopFilePath = Path.Combine(debUsrAppFolderPath, $"{ExecutableName}.desktop");
					var desktopFileContent = $"[Desktop Entry]\n" +
						$"Type=Application\n" +
						$"Name=Wasabi Wallet\n" +
						$"StartupWMClass=Wasabi Wallet\n" +
						$"GenericName=Bitcoin Wallet\n" +
						$"Comment=Privacy focused Bitcoin wallet.\n" +
						$"Icon={ExecutableName}\n" +
						$"Terminal=false\n" +
						$"Exec={ExecutableName}\n" +
						$"Categories=Office;Finance;\n" +
						$"Keywords=bitcoin;wallet;crypto;blockchain;wasabi;privacy;anon;awesome;qwe;asd;\n";

					File.WriteAllText(desktopFilePath, desktopFileContent, Encoding.ASCII);

					var wasabiStarterScriptPath = Path.Combine(debUsrLocalBinFolderPath, $"{ExecutableName}");
					var wasabiStarterScriptContent = $"#!/bin/sh\n" +
						$"{ linuxWasabiWalletFolder.TrimEnd('/')}/{ExecutableName} $@\n";

					File.WriteAllText(wasabiStarterScriptPath, wasabiStarterScriptContent, Encoding.ASCII);

					string debExeLinuxPath = Tools.LinuxPathCombine(newFolderRelativePath, ExecutableName);
					string debDestopFileLinuxPath = Tools.LinuxPathCombine(debUsrAppFolderRelativePath, $"{ExecutableName}.desktop");

					commands = new[]
					{
						"cd ~",
						"sudo umount /mnt/c",
						"sudo mount -t drvfs C: /mnt/c -o metadata",
						$"cd {linuxPath}",
						$"sudo find {Tools.LinuxPath(newFolderRelativePath)} -type f -exec chmod 644 {{}} \\;",
						$"sudo find {Tools.LinuxPath(newFolderRelativePath)} {chmodExecutablesArgs}",
						$"sudo chmod -R 0775 {Tools.LinuxPath(debianFolderRelativePath)}",
						$"sudo chmod -R 0644 {debDestopFileLinuxPath}",
						$"dpkg --build {Tools.LinuxPath(debFolderRelativePath)} $(pwd)"
					};
					arguments = string.Join(" && ", commands);

					StartProcessAndWaitForExit("wsl", BinDistDirectory, arguments: arguments);

					IoHelpers.TryDeleteDirectoryAsync(debFolderPath).GetAwaiter().GetResult();

					string oldDeb = Path.Combine(BinDistDirectory, $"{ExecutableName}_{VersionPrefix}_amd64.deb");
					string newDeb = Path.Combine(BinDistDirectory, $"Wasabi-{VersionPrefix}.deb");
					File.Move(oldDeb, newDeb);

					IoHelpers.TryDeleteDirectoryAsync(publishedFolder).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {publishedFolder}");
				}
			}
		}

		private static string? StartProcessAndWaitForExit(string command, string workingDirectory, string? writeToStandardInput = null, string? arguments = null, bool redirectStandardOutput = false)
		{
			var isWriteToStandardInput = !string.IsNullOrWhiteSpace(writeToStandardInput);

			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = command,
				Arguments = arguments ?? "",
				RedirectStandardInput = isWriteToStandardInput,
				RedirectStandardOutput = redirectStandardOutput,
				WorkingDirectory = workingDirectory
			});

			if (process is null)
			{
				throw new InvalidOperationException($"Process '{command}' is invalid.");
			}

			if (isWriteToStandardInput)
			{
				process.StandardInput.WriteLine(writeToStandardInput);
			}

			string? output = null;

			if (redirectStandardOutput)
			{
				output = process.StandardOutput.ReadToEnd();
			}

			process.WaitForExit();

			if (process.ExitCode is not 0)
			{
				Console.WriteLine($"Process failed:");
				Console.WriteLine($"* Command: '{command} {arguments}'");
				Console.WriteLine($"* Working directory: '{workingDirectory}'");
				Console.WriteLine($"* Exit code: '{process.ExitCode}'");

				if (redirectStandardOutput)
				{
					string prettyPrint = string.Join(Environment.NewLine, (output ?? "").Split(Environment.NewLine).Select(line => $"  > {line}"));
					Console.WriteLine($"* Output:\n{prettyPrint}");
				}

				throw new InvalidOperationException("Process exited with unexpected exit code");
			}

			return output;
		}

#pragma warning restore CS0162 // Unreachable code detected
	}
}
