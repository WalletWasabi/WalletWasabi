using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
	class Daemon
	{
		internal static async Task RunAsync(string walletName, LogLevel? logLevel, bool mixAll, bool keepMixAlive, bool silent)
		{
			Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));

			if (logLevel.HasValue)
			{
				Logger.SetMinimumLevel(logLevel.Value);
			}
			if (silent)
			{
				Logger.Modes.Remove(LogMode.Console);
				Logger.Modes.Remove(LogMode.Debug);
			}
			else
			{
				Logger.Modes.Add(LogMode.Console);
				Logger.Modes.Add(LogMode.Debug);
			}
			Logger.LogStarting("Wasabi");

			KeyManager keyManager = null;
			if (walletName != null)
			{
				var walletFullPath = Global.GetWalletFullPath(walletName);
				var walletBackupFullPath = Global.GetWalletBackupFullPath(walletName);
				if (!File.Exists(walletFullPath) && !File.Exists(walletBackupFullPath))
				{
					// The selected wallet is not available any more (someone deleted it?).
					Logger.LogCritical("The selected wallet doesn't exsist, did you delete it?", nameof(Daemon));
					return;
				}

				try
				{
					keyManager = Global.LoadKeyManager(walletFullPath, walletBackupFullPath);
				}
				catch (Exception ex)
				{
					Logger.LogCritical(ex, nameof(Daemon));
					return;
				}
			}

			if (keyManager is null)
			{
				Logger.LogCritical("Wallet was not supplied. Add --wallet {WalletName}", nameof(Daemon));
				return;
			}

			string password = null;
			var count = 3;
			do
			{
				if (password != null)
				{
					if (count > 0)
					{
						Logger.LogError($"Wrong password. {count} attempts left. Try again.");
					}
					else
					{
						Logger.LogCritical($"Wrong password. {count} attempts left. Exiting...");
						return;
					}
					count--;
				}
				Console.Write("Password: ");

				password = PasswordConsole.ReadPassword();
				password = Guard.Correct(password);
			}
			while (!keyManager.TestPassword(password));

			Logger.LogInfo("Correct password.");

			await Global.InitializeNoUiAsync();
			await Global.InitializeWalletServiceAsync(keyManager);

			await TryQueueCoinsToMixAsync(mixAll, password);

			var mixing = true;
			do
			{
				if (Global.KillRequested) break;
				await Task.Delay(3000);
				if (Global.KillRequested) break;

				bool anyCoinsQueued = Global.ChaumianClient.State.AnyCoinsQueued();

				if (!anyCoinsQueued && keepMixAlive) // If no coins queued and mixing is asked to be kept alive then try to queue coins.
				{
					await TryQueueCoinsToMixAsync(mixAll, password);
				}

				if (Global.KillRequested) break;

				mixing = anyCoinsQueued || keepMixAlive;
			} while (mixing);

			await Global.ChaumianClient.DequeueAllCoinsFromMixAsync();
		}

		private static async Task TryQueueCoinsToMixAsync(bool mixAll, string password)
		{
			try
			{
				if (mixAll)
				{
					await Global.ChaumianClient.QueueCoinsToMixAsync(password, Global.WalletService.Coins.Where(x => !x.Unavailable).ToArray());
				}
				else
				{
					await Global.ChaumianClient.QueueCoinsToMixAsync(password, Global.WalletService.Coins.Where(x => !x.Unavailable && x.AnonymitySet < Global.WalletService.ServiceConfiguration.MixUntilAnonymitySet).ToArray());
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, nameof(Daemon));
			}
		}
	}
}
