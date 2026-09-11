using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WalletWasabi.BundledApps;

/// <summary>
/// Factory for <see cref="ProcessStartInfo"/> with pre-defined properties as needed in Wasabi Wallet.
/// </summary>
public class ProcessStartInfoFactory
{
	/// <summary>
	/// Creates new <see cref="ProcessStartInfo"/> instance using ArgumentList for proper escaping.
	/// This is the preferred overload for security-sensitive operations like HWI where user-provided
	/// data may flow into arguments.
	/// </summary>
	/// <param name="processPath">Path to process.</param>
	/// <param name="arguments">Process arguments as separate elements (each will be properly escaped).</param>
	/// <param name="openConsole">Open console window. Only for Windows platform.</param>
	/// <returns><see cref="ProcessStartInfo"/> instance.</returns>
	public static ProcessStartInfo Make(string processPath, IReadOnlyList<string> arguments, bool openConsole = false)
	{
		if (openConsole && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			throw new PlatformNotSupportedException($"{RuntimeInformation.OSDescription} is not supported.");
		}

		var p = new ProcessStartInfo
		{
			FileName = processPath,
			RedirectStandardOutput = !openConsole,
			RedirectStandardError = !openConsole,
			UseShellExecute = openConsole,
			CreateNoWindow = !openConsole,
			WindowStyle = ProcessWindowStyle.Normal
		};

		foreach (var arg in arguments)
		{
			p.ArgumentList.Add(arg);
		}

		return p;
	}
}
