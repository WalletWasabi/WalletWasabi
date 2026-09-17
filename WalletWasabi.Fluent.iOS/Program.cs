using System;
using System.IO;
using Foundation;
using UIKit;

namespace WalletWasabi.Fluent.IOS;

public static class Program
{
	[Preserve(AllMembers = true)]
	public static void Main(string[] args)
	{
		string logFile;
		try
		{
			var docPath = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User)[0].Path;
			Directory.CreateDirectory(docPath);
			logFile = Path.Combine(docPath, "wasabi_ios.log");
		}
		catch
		{
			logFile = "/tmp/wasabi_ios.log";
		}

		void LogMessage(string msg)
		{
			try
			{
				var simSharedDir = Environment.GetEnvironmentVariable("SIMULATOR_SHARED_RESOURCES_DIRECTORY") ?? "/tmp";
				var sharedLogFile = Path.Combine(simSharedDir, "wasabi_ios.log");
				File.AppendAllText(sharedLogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
				Console.WriteLine($"[WASABI_IOS] {msg}");
			}
			catch { }
		}

		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			LogMessage($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
		};

		System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
		{
			LogMessage($"UNOBSERVED TASK EXCEPTION: {e.Exception}");
		};

		LogMessage("Program.Main starting...");
		try
		{
			UIApplication.Main(args, null, "AppDelegate");
		}
		catch (Exception ex)
		{
			LogMessage($"Program.Main EXCEPTION: {ex}");
			throw;
		}
	}
}
