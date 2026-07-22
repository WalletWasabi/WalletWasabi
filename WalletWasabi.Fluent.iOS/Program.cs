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
		var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		var logFile = Path.Combine(docPath, "wasabi_ios.log");

		void LogMessage(string msg)
		{
			try
			{
				File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
				Console.WriteLine($"[WASABI_IOS] {msg}");
			}
			catch { }
		}

		AppDomain.CurrentDomain.UnhandledException += (s, e) =>
		{
			LogMessage($"CRITICAL UNHANDLED EXCEPTION: {e.ExceptionObject}");
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
