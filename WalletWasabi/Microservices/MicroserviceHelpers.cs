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
			var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

			string commonPartialPath = Path.Combine(fullBaseDirectory, "Microservices", "Binaries");
			string path;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				path = Path.Combine(commonPartialPath, $"win64");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				path = Path.Combine(commonPartialPath, $"lin64");
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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
