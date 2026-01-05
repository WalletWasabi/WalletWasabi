using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tor;

namespace WalletWasabi.Tests.Helpers;

public static class Common
{
	static Common()
	{
		Logger.SetFilePath(Path.Combine(DataDir, "Logs.txt"));
		Logger.SetMinimumLevel(LogLevel.Info);
		Logger.SetModes(LogMode.Debug, LogMode.File);
	}

	public static EndPoint TorSocks5Endpoint => new IPEndPoint(IPAddress.Loopback, 37150);
	public static string TorDistributionFolder => Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "TorDaemons");

	/// <remarks>Tor is instructed to terminate on exit because this Tor instance would prevent running your Wasabi Wallet where Tor is started with data in a different folder.</remarks>
	public static TorSettings TorSettings => new(TorBackend.CTor, DataDir, TorDistributionFolder, terminateOnExit: true);

	public static string DataDir => EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Tests"));

	public static string GetWorkDir([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		return Path.Combine(DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName);
	}

	/// <summary>
	/// Gets an empty directory for test to work with.
	/// </summary>
	/// <remarks>If the directory exists, its content is removed.</remarks>
	public static async Task<string> GetEmptyWorkDirAsync([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
	{
		string workDirectory = GetWorkDir(callerFilePath, callerMemberName);

		if (Directory.Exists(workDirectory))
		{
			await IoHelpers.TryDeleteDirectoryAsync(workDirectory).ConfigureAwait(false);
		}

		Directory.CreateDirectory(workDirectory);

		return workDirectory;
	}

	public static IEnumerable<TResult> Repeat<TResult>(Func<TResult> action, int count)
	{
		for (int i = 0; i < count; i++)
		{
			yield return action();
		}
	}
}
