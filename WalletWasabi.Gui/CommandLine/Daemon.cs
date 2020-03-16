using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.CommandLine
{
	public class Daemon
	{
		public Daemon(Global global)
		{
			Global = global;
		}

		private Global Global { get; }

		private WalletService WalletService { get; set; }

		internal async Task RunAsync(string walletName, string destinationWalletName, bool keepMixAlive)
		{
			try
			{
				Logger.LogSoftwareStarted("Wasabi Daemon");

				KeyManager keyManager = TryGetKeyManagerFromWalletName(walletName);
				if (keyManager is null)
				{
					return;
				}

				string password = null;
				var count = 3;
				string compatibilityPassword = null;
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
					if (PasswordHelper.IsTooLong(password, out password))
					{
						Console.WriteLine(PasswordHelper.PasswordTooLongMessage);
					}
					if (PasswordHelper.IsTrimable(password, out password))
					{
						Console.WriteLine(PasswordHelper.TrimWarnMessage);
					}
				}
				while (!PasswordHelper.TryPassword(keyManager, password, out compatibilityPassword));

				if (compatibilityPassword != null)
				{
					password = compatibilityPassword;
					Logger.LogInfo(PasswordHelper.CompatibilityPasswordWarnMessage);
				}

				Logger.LogInfo("Correct password.");

				await Global.InitializeNoWalletAsync();
				if (Global.KillRequested)
				{
					return;
				}

				WalletService = await Global.WalletManager.CreateAndStartWalletServiceAsync(keyManager);
				if (Global.KillRequested)
				{
					return;
				}

				KeyManager destinationKeyManager = TryGetKeyManagerFromWalletName(destinationWalletName);
				bool isDifferentDestinationSpecified = keyManager.ExtPubKey != destinationKeyManager.ExtPubKey;
				if (isDifferentDestinationSpecified)
				{
					await Global.WalletManager.CreateAndStartWalletServiceAsync(destinationKeyManager);
				}

				do
				{
					if (Global.KillRequested)
					{
						break;
					}

					// If no coins enqueued then try to enqueue the large anonset coins and mix to another wallet.
					if (isDifferentDestinationSpecified && !AnyCoinsQueued())
					{
						WalletService.ChaumianClient.DestinationKeyManager = destinationKeyManager;
						await TryQueueCoinsToMixAsync(password, minAnonset: WalletService.ServiceConfiguration.MixUntilAnonymitySet);
					}

					if (Global.KillRequested)
					{
						break;
					}

					// If no coins were enqueued then try to enqueue coins those have less anonset and mix into the same wallet.
					if (!AnyCoinsQueued())
					{
						WalletService.ChaumianClient.DestinationKeyManager = WalletService.ChaumianClient.KeyManager;
						await TryQueueCoinsToMixAsync(password, maxAnonset: WalletService.ServiceConfiguration.MixUntilAnonymitySet - 1);
					}

					if (Global.KillRequested)
					{
						break;
					}

					await Task.Delay(3000);
				}
				// Keep this loop alive as long as a coin is enqueued or keepalive was specified.
				while (keepMixAlive || AnyCoinsQueued());

				await Global.DisposeAsync();
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

		private bool AnyCoinsQueued()
		{
			return WalletService.ChaumianClient.State.AnyCoinsQueued();
		}

		public KeyManager TryGetKeyManagerFromWalletName(string walletName)
		{
			try
			{
				KeyManager keyManager = null;
				if (walletName != null)
				{
					try
					{
						keyManager = Global.LoadKeyManager(walletName);
					}
					catch (FileNotFoundException)
					{
						Logger.LogCritical("The selected wallet does not exist, did you delete it?");
						return null;
					}
					catch (Exception ex)
					{
						Logger.LogCritical(ex);
						return null;
					}
				}

				if (keyManager is null)
				{
					Logger.LogCritical("Wallet was not supplied. Add --wallet:WalletName");
				}

				return keyManager;
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex);
				return null;
			}
		}

		private async Task TryQueueCoinsToMixAsync(string password, int minAnonset = int.MinValue, int maxAnonset = int.MaxValue)
		{
			try
			{
				var coinsToMix = WalletService.Coins.Available().FilterBy(x => x.AnonymitySet <= maxAnonset && minAnonset <= x.AnonymitySet);

				var enqueuedCoins = await WalletService.ChaumianClient.QueueCoinsToMixAsync(password, coinsToMix.ToArray());

				if (enqueuedCoins.Any())
				{
					Logger.LogInfo($"Enqueued {Money.Satoshis(enqueuedCoins.Sum(x => x.Amount)).ToString(false, true)} BTC, {enqueuedCoins.Count()} coins with smalles anonset {enqueuedCoins.Min(x => x.AnonymitySet)} and largest anonset {enqueuedCoins.Max(x => x.AnonymitySet)}.");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}
	}
}
