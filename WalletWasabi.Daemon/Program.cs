using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Daemon;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
		var app = WasabiAppBuilder
			.Create("Wasabi Daemon", args)
			.EnsureSingleInstance()
			.OnUnhandledExceptions(LogUnhandledException)
			.OnUnobservedTaskExceptions(LogUnobservedTaskException)
			.Build();

		var exitCode = await app.RunAsConsoleAsync();
		return (int)exitCode;
	}

	private static void LogUnobservedTaskException(object? sender, AggregateException e)
	{
		ReadOnlyCollection<Exception> innerExceptions = e.Flatten().InnerExceptions;

		switch (innerExceptions)
		{
			case [SocketException { SocketErrorCode: SocketError.OperationAborted }]:
			// Source of this exception is NBitcoin library.
			case [OperationCanceledException { Message: "The peer has been disconnected" }]:
				// Until https://github.com/MetacoSA/NBitcoin/pull/1089 is resolved.
				Logger.LogTrace(e);
				break;
			default:
				Logger.LogDebug(e);
				break;
		}
	}

	private static void LogUnhandledException(object? sender, Exception e) =>
		Logger.LogWarning(e);
}

public static class WasabiAppExtensions
{
	public static async Task<ExitCode> RunAsConsoleAsync(this WasabiApplication app)
	{
		void ProcessCommands()
		{
			var arguments = app.AppConfig.Arguments;
			var walletNames = ArgumentHelpers
				.GetValues("wallet", arguments, x => x)
				.Distinct();

			foreach (var walletName in walletNames)
			{
				try
				{
					var wallet = app.Global!.WalletManager.GetWalletByName(walletName);
					app.Global!.WalletManager.StartWalletAsync(wallet).ConfigureAwait(false);
				}
				catch (InvalidOperationException)
				{
					Logger.LogWarning($"Wallet '{walletName}' was not found. Ignoring...");
				}
			}
		}

		return await app.RunAsync(
			async () =>
			{
				await app.Global!.InitializeNoWalletAsync(app.TerminateService).ConfigureAwait(false);

				ProcessCommands();

				while (true)
				{
					Console.Read();
				}
			});
	}
}
