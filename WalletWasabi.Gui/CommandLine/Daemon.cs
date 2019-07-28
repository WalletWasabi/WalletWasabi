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
	public class Daemon
	{
		public Global Global { get; }

		public Daemon(Global global)
		{
			Global = global;
		}

		internal async Task RunAsync(string walletName, bool mixAll, bool keepMixAlive)
		{
			try
			{
				Logger.LogStarting("Wasabi Daemon");

				KeyManager keyManager = TryGetKeyManagerFromWalletName(walletName);
				if (keyManager is null)
				{
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

				await Global.InitializeNoWalletAsync();
				if (Global.KillRequested)
				{
					return;
				}

				await Global.InitializeWalletServiceAsync(keyManager);
				if (Global.KillRequested)
				{
					return;
				}

				await TryQueueCoinsToMixAsync(mixAll, password);

				bool mixing;
				do
				{
					if (Global.KillRequested)
					{
						break;
					}

					await Task.Delay(3000);
					if (Global.KillRequested)
					{
						break;
					}

					bool anyCoinsQueued = Global.ChaumianClient.State.AnyCoinsQueued();
					if (!anyCoinsQueued && keepMixAlive) // If no coins queued and mixing is asked to be kept alive then try to queue coins.
					{
						await TryQueueCoinsToMixAsync(mixAll, password);
					}

					if (Global.KillRequested)
					{
						break;
					}

					mixing = anyCoinsQueued || keepMixAlive;
				} while (mixing);

				if (!Global.KillRequested) // This only has to run if it finishes by itself. Otherwise the Ctrl+c runs it.
				{
					await Global.ChaumianClient?.DequeueAllCoinsFromMixAsync("Stopping Wasabi.");
				}
			}
			catch
			{
				if (!Global.KillRequested)
				{
					throw;
				}
			}
			finally
			{
				Logger.LogInfo($"{nameof(Daemon)} stopped.");
			}
		}

		public KeyManager TryGetKeyManagerFromWalletName(string walletName)
		{
			try
			{
				KeyManager keyManager = null;
				if (walletName != null)
				{
					var walletFullPath = Global.GetWalletFullPath(walletName);
					var walletBackupFullPath = Global.GetWalletBackupFullPath(walletName);
					if (!File.Exists(walletFullPath) && !File.Exists(walletBackupFullPath))
					{
						// The selected wallet is not available any more (someone deleted it?).
						Logger.LogCritical("The selected wallet does not exist, did you delete it?", nameof(Daemon));
						return null;
					}

					try
					{
						keyManager = Global.LoadKeyManager(walletFullPath, walletBackupFullPath);
					}
					catch (Exception ex)
					{
						Logger.LogCritical(ex, nameof(Daemon));
						return null;
					}
				}

				if (keyManager is null)
				{
					Logger.LogCritical("Wallet was not supplied. Add --wallet:WalletName", nameof(Daemon));
				}

				return keyManager;
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex, nameof(Daemon));
				return null;
			}
		}

		private async Task TryQueueCoinsToMixAsync(bool mixAll, string password)
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
