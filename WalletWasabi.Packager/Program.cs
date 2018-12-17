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
			// 3. Sign with Packager, set restore true so the password won't be kept.
			var doPublish = true;
			var doSign = false;
			var doRestoreThisFile = false;
			var pfxPassword = "dontcommit";

			string pfxPath = "C:\\digicert.pfx";
			string packagerProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\"));
			string solutionDirectory = Path.GetFullPath(Path.Combine(packagerProjectDirectory, "..\\"));
			string guiProjectDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "WalletWasabi.Gui\\"));
			string libraryProjectDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "WalletWasabi\\"));
			string wixProjectDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "WalletWasabi.WindowsInstaller\\"));
			string binDistDirectory = Path.GetFullPath(Path.Combine(guiProjectDirectory, "bin\\dist"));
			Console.WriteLine($"{nameof(solutionDirectory)}:\t\t{solutionDirectory}");
			Console.WriteLine($"{nameof(packagerProjectDirectory)}:\t{packagerProjectDirectory}");
			Console.WriteLine($"{nameof(guiProjectDirectory)}:\t\t{guiProjectDirectory}");
			Console.WriteLine($"{nameof(libraryProjectDirectory)}:\t\t{libraryProjectDirectory}");
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
				using (var pBuild = Process.Start(psiBuild))
				{
					pBuild.StandardInput.WriteLine("dotnet clean --configuration Release && exit");
					pBuild.WaitForExit();
				}

				var guiBinReleaseDirectory = Path.GetFullPath(Path.Combine(guiProjectDirectory, "bin\\Release"));
				var libraryBinReleaseDirectory = Path.GetFullPath(Path.Combine(libraryProjectDirectory, "bin\\Release"));
				if (Directory.Exists(guiBinReleaseDirectory))
				{
					IoHelpers.DeleteRecursivelyWithMagicDustAsync(guiBinReleaseDirectory).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {guiBinReleaseDirectory}");
				}
				if (Directory.Exists(libraryBinReleaseDirectory))
				{
					IoHelpers.DeleteRecursivelyWithMagicDustAsync(libraryBinReleaseDirectory).GetAwaiter().GetResult();
					Console.WriteLine($"Deleted {libraryBinReleaseDirectory}");
				}

				foreach (string target in targets)
				{
					string currentBinDistDirectory;
					string targetDir = Path.Combine(binDistDirectory, target);
					string macWasabiAppDir = Path.Combine(targetDir, "Wasabi Wallet.App");
					string macContentsDir = Path.Combine(macWasabiAppDir, "Contents");
					if (target.StartsWith("osx"))
					{
						currentBinDistDirectory = Path.GetFullPath(Path.Combine(macContentsDir, "MacOS"));
					}
					else
					{
						currentBinDistDirectory = targetDir;
					}
					Console.WriteLine();
					Console.WriteLine($"{nameof(currentBinDistDirectory)}:\t{currentBinDistDirectory}");

					Console.WriteLine();
					if (!Directory.Exists(currentBinDistDirectory))
					{
						Directory.CreateDirectory(currentBinDistDirectory);
						Console.WriteLine($"Created {currentBinDistDirectory}");
					}

					var psiClean = new ProcessStartInfo
					{
						FileName = "dotnet",
						Arguments = $"clean",
						WorkingDirectory = guiProjectDirectory
					};
					using (var pClean = Process.Start(psiClean))
					{
						pClean.WaitForExit();
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
						Arguments = $"publish --configuration Release --force --output \"{currentBinDistDirectory}\" --self-contained true --runtime \"{target}\" /p:VersionPrefix={versionPrefix} --disable-parallel --no-cache",
						WorkingDirectory = guiProjectDirectory
					};
					using (var pPublish = Process.Start(psiPublish))
					{
						pPublish.WaitForExit();
					}

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
						using (var pEditbin = Process.Start(psiEditbin))
						{
							pEditbin.WaitForExit();
						}

						var icoPath = Path.Combine(guiProjectDirectory, "Assets", "WasabiLogo.ico");
						var psiRcedit = new ProcessStartInfo
						{
							FileName = "rcedit",
							Arguments = $"\"{newExecutablePath}\" --set-icon \"{icoPath}\" --set-file-version \"{versionPrefix}\" --set-product-version \"{versionPrefix}\" --set-version-string \"LegalCopyright\" \"MIT\" --set-version-string \"CompanyName\" \"zkSNACKs\" --set-version-string \"FileDescription\" \"Privacy focused, ZeroLink compliant Bitcoin wallet.\" --set-version-string \"ProductName\" \"Wasabi Wallet\"",
							WorkingDirectory = currentBinDistDirectory
						};
						using (var pRcedit = Process.Start(psiRcedit))
						{
							pRcedit.WaitForExit();
						}
					}
					else if (target.StartsWith("osx"))
					{
						string resourcesDir = Path.Combine(macContentsDir, "Resources");
						string infoFilePath = Path.Combine(macContentsDir, "Info.plist");

						Directory.CreateDirectory(resourcesDir);
						var iconpath = Path.Combine(guiProjectDirectory, "Assets", "WasabiLogo.icns");
						File.Copy(iconpath, Path.Combine(resourcesDir, "WasabiLogo.icns"));

						string infoContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version = ""1.0"">
<dict>
	<key>LSMinimumSystemVersion</key>
	<string>10.12</string>

	<key>LSArchitecturePriority</key>
	<array>
		<string>x86_64</string>
	</array>

	<key>CFBundleIconFile</key>
	<string>WasabiLogo.icns</string>

	<key>CFBundlePackageType</key>
	<string>APPL</string>

	<key>CFBundleShortVersionString</key>
	<string>{versionPrefix}</string>

	<key>CFBundleVersion</key>
	<string>{versionPrefix}</string>

	<key>CFBundleExecutable</key>
	<string>wassabee</string>

	<key>CFBundleName</key>
	<string>Wasabi Wallet</string>

	<key>CFBundleIdentifier</key>
	<string>zksnacks.wasabiwallet</string>

	<key>NSHighResolutionCapable</key>
	<true/>

	<key>NSAppleScriptEnabled</key>
	<true/>

	<key>LSApplicationCategoryType</key>
	<string>public.app-category.finance</string>

	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
</dict>
</plist>
";
						File.WriteAllText(infoFilePath, infoContent);

						var psiCreateSymLink = new ProcessStartInfo
						{
							FileName = "cmd",
							RedirectStandardInput = true,
							WorkingDirectory = targetDir
						};
						using (var createSymLinkProcess = Process.Start(psiCreateSymLink))
						{
							createSymLinkProcess.StandardInput.WriteLine($"wsl ln -s /Applications && exit");
							createSymLinkProcess.WaitForExit();
						}

						//how to generate .DS_Store file - https://github.com/zkSNACKs/WalletWasabi/pull/928/commits/e38ed672dee25f6e45a3eb16584887cc6d48c4e6
						var dmgContentDir = Path.Combine(packagerProjectDirectory, "Content", "Osx");
						IoHelpers.CopyFilesRecursively(new DirectoryInfo(dmgContentDir), new DirectoryInfo(targetDir));

						var psiGenIsoImage = new ProcessStartInfo
						{
							FileName = "cmd",
							RedirectStandardInput = true,
							WorkingDirectory = binDistDirectory
						};

						string uncompressedDmgFileName = $"Wasabi-uncompressed.dmg";
						string uncompressedDmgFilePath = Path.Combine(binDistDirectory, uncompressedDmgFileName);
						string dmgFileName = $"Wasabi-{versionPrefix}.dmg";
						using (var genIsoImageProcess = Process.Start(psiGenIsoImage))
						{
							// http://www.nathancoulson.com/proj_cross_tools.php
							// -D: Do not use deep directory relocation, and instead just pack them in the way we see them
							// -V: Volume Label
							// -no-pad: Do not pad the end by 150 sectors (300kb). As it is not a cd image, not required
							// -apple -r: Creates a .dmg image
							genIsoImageProcess.StandardInput.WriteLine($"wsl genisoimage -D -V \"Wasabi Wallet\" -no-pad -apple -r -o \"{uncompressedDmgFileName}\" \"{new DirectoryInfo(targetDir).Name}\" && exit");
							genIsoImageProcess.WaitForExit();
						}
						// cd ~
						// git clone https://github.com/planetbeing/libdmg-hfsplus.git && cd libdmg-hfsplus
						// https://github.com/planetbeing/libdmg-hfsplus/issues/14
						// mkdir build && cd build
						// sudo apt-get install zlib1g-dev
						// cmake ..
						// cd build
						// sudo apt-get install libssl1.0-dev
						// cmake ..
						// cd ~/libdmg-hfsplus/build/
						// make
						var psiDmg = new ProcessStartInfo
						{
							FileName = "cmd",
							RedirectStandardInput = true,
							WorkingDirectory = binDistDirectory
						};
						using (var dmgProcess = Process.Start(psiDmg))
						{
							dmgProcess.StandardInput.WriteLine($"wsl ~/libdmg-hfsplus/build/dmg/./dmg dmg \"{uncompressedDmgFileName}\" \"{dmgFileName}\" && exit");
							dmgProcess.WaitForExit();
						}
						// In case compression above doesn't work:
						//var psiBzip = new ProcessStartInfo
						//{
						//	FileName = "cmd",
						//	RedirectStandardInput = true,
						//	WorkingDirectory = binDistDirectory
						//};
						//var bzipProcess = Process.Start(psiBzip);
						//bzipProcess.StandardInput.WriteLine($"wsl bzip2 \"{uncompressedDmgFileName}\" && exit");
						//bzipProcess.WaitForExit();

						IoHelpers.DeleteRecursivelyWithMagicDustAsync(targetDir).GetAwaiter().GetResult();
						File.Delete(uncompressedDmgFilePath);
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
						var msiPath = Path.Combine(wixProjectDirectory, @"bin\Release\Wasabi.msi");
						if (!File.Exists(msiPath))
						{
							throw new Exception(".msi doesn't exist. Expected path: Wasabi.msi.");
						}
						var msiFileName = Path.GetFileNameWithoutExtension(msiPath);
						var newMsiPath = Path.Combine(binDistDirectory, $"{msiFileName}-{versionPrefix}.msi");
						File.Move(msiPath, newMsiPath);

						// Sign code with digicert.
						var psiSigntool = new ProcessStartInfo
						{
							FileName = "cmd",
							RedirectStandardInput = true,
							WorkingDirectory = binDistDirectory
						};
						using (var signToolProcess = Process.Start(psiSigntool))
						{
							signToolProcess.StandardInput.WriteLine($"signtool sign /d \"Wasabi Wallet\" /f \"{pfxPath}\" /p {pfxPassword} /t http://timestamp.digicert.com /a \"{newMsiPath}\" && exit");
							signToolProcess.WaitForExit();
						}
					}
					else if (target.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine("Create Linux .tar.gz");
						if (!Directory.Exists(publishedFolder))
						{
							throw new Exception($"{publishedFolder} doesn't exist.");
						}
						var newFolderName = $"WasabiLinux-{versionPrefix}";
						var newFolderPath = Path.Combine(binDistDirectory, newFolderName);
						Directory.Move(publishedFolder, newFolderPath);
						publishedFolder = newFolderPath;

						var psiTar = new ProcessStartInfo
						{
							FileName = "cmd",
							RedirectStandardInput = true,
							WorkingDirectory = binDistDirectory
						};
						using (var tarProcess = Process.Start(psiTar))
						{
							tarProcess.StandardInput.WriteLine($"wsl tar -pczvf {newFolderName}.tar.gz {newFolderName} && exit");
							tarProcess.WaitForExit();
						}
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
					using (var signProcess = Process.Start(psiSignProcess))
					{
						signProcess.StandardInput.WriteLine($"gpg --armor --detach-sign {finalFile} && exit");
						signProcess.WaitForExit();
					}

					var psiRestoreHeat = new ProcessStartInfo
					{
						FileName = "cmd",
						RedirectStandardInput = true,
						WorkingDirectory = wixProjectDirectory
					};
					using (var restoreHeatProcess = Process.Start(psiRestoreHeat))
					{
						restoreHeatProcess.StandardInput.WriteLine($"git checkout -- ComponentsGenerated.wxs && exit");
						restoreHeatProcess.WaitForExit();
					}

					if (doRestoreThisFile)
					{
						var psiRestoreThisFile = new ProcessStartInfo
						{
							FileName = "cmd",
							RedirectStandardInput = true,
							WorkingDirectory = packagerProjectDirectory
						};
						using (var restoreThisFileProcess = Process.Start(psiRestoreThisFile))
						{
							restoreThisFileProcess.StandardInput.WriteLine($"git checkout -- Program.cs && exit");
							restoreThisFileProcess.WaitForExit();
						}
					}
				}

				IoHelpers.OpenFolderInFileExplorer(binDistDirectory);
				return; // No need for readkey here.
			}

			Console.WriteLine();
			Console.WriteLine("FINISHED! Press key to exit...");
			Console.ReadKey();
		}
	}
}
