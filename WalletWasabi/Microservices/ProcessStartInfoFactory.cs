using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WalletWasabi.Microservices
{
	public class ProcessStartInfoFactory
	{
		public static ProcessStartInfo Make(string processPath, string arguments, bool openConsole = false)
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

			var p = new ProcessStartInfo(fileName: processPath, arguments);
			p.FileName = processPath;
			p.Arguments = arguments;
			p.RedirectStandardOutput = !openConsole;
			p.UseShellExecute = openConsole;
			p.CreateNoWindow = !openConsole;
			p.WindowStyle = windowStyle;

			return p;
		}
	}
}
