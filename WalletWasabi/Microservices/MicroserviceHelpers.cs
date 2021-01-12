using System;
using System.IO;
using System.Runtime.InteropServices;
using WalletWasabi.Helpers;

namespace WalletWasabi.Microservices
{
	public static class MicroserviceHelpers
	{
		public static string GetBinaryFolder()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return GetBinaryFolder(OSPlatform.Windows);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				return GetBinaryFolder(OSPlatform.Linux);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return GetBinaryFolder(OSPlatform.OSX);
			}
			else
			{
				throw new NotSupportedException("Operating system is not supported.");
			}
		}

		public static string GetBinaryFolder(OSPlatform platform)
		{
			var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

			string commonPartialPath = Path.Combine(fullBaseDirectory, "Microservices", "Binaries");
			string path;
			if (platform == OSPlatform.Windows)
			{
				path = Path.Combine(commonPartialPath, $"win64");
			}
			else if (platform == OSPlatform.Linux)
			{
				path = Path.Combine(commonPartialPath, $"lin64");
			}
			else if (platform == OSPlatform.OSX)
			{
				path = Path.Combine(commonPartialPath, $"osx64");
			}
			else
			{
				throw new NotSupportedException("Operating system is not supported.");
			}

			return path;
		}

		public static string GetBinaryPath(string binaryNameWithoutExtension)
		{
			return Path.Combine(GetBinaryFolder(), RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{binaryNameWithoutExtension}.exe" : $"{binaryNameWithoutExtension}");
		}
	}
}
