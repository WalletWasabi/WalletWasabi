using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.CommandLine
{
	public class Daemon
	{
		public Daemon(Global global)
		{
			Global = global;
		}

		private Global Global { get; }

		private Wallet Wallet { get; set; }

		internal async Task RunAsync(string walletName, string destinationWalletName, bool keepMixAlive)
		{
			try
			{
				Logger.LogSoftwareStarted("Wasabi Daemon");

				KeyManager keyManager = Global.WalletManager.GetWalletByName(walletName).KeyManager;

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

				Wallet = await Global.WalletManager.StartWalletAsync(keyManager);
				if (Global.KillRequested)
				{
					return;
				}

				KeyManager destinationKeyManager = Global.WalletManager.GetWalletByName(destinationWalletName).KeyManager;
				bool isDifferentDestinationSpecified = keyManager.ExtPubKey != destinationKeyManager.ExtPubKey;
				if (isDifferentDestinationSpecified)
				{
					await Global.WalletManager.StartWalletAsync(destinationKeyManager);
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
						Wallet.ChaumianClient.DestinationKeyManager = destinationKeyManager;
						await TryQueueCoinsToMixAsync(password, minAnonset: Wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue());
					}

					if (Global.KillRequested)
					{
						break;
					}

					// If no coins were enqueued then try to enqueue coins those have less anonset and mix into the same wallet.
					if (!AnyCoinsQueued())
					{
						Wallet.ChaumianClient.DestinationKeyManager = Wallet.ChaumianClient.KeyManager;
						await TryQueueCoinsToMixAsync(password, maxAnonset: Wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue() - 1);
					}

					if (Global.KillRequested)
					{
						break;
					}

					await Task.Delay(3000);
				}

				// Keep this loop alive as long as a coin is enqueued or keepalive was specified.
				while (keepMixAlive || AnyCoinsQueued());
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
				await Global.DisposeAsync().ConfigureAwait(false); 
				Logger.LogInfo($"{nameof(Daemon)} stopped.");
			}
		}

		private bool AnyCoinsQueued()
		{
			return Wallet.ChaumianClient.State.AnyCoinsQueued();
		}

		private async Task TryQueueCoinsToMixAsync(string password, int minAnonset = int.MinValue, int maxAnonset = int.MaxValue)
		{
			try
			{
				var coinsToMix = Wallet.Coins.Available().FilterBy(x => x.AnonymitySet <= maxAnonset && minAnonset <= x.AnonymitySet);

				var enqueuedCoins = await Wallet.ChaumianClient.QueueCoinsToMixAsync(password, coinsToMix.ToArray());

				if (enqueuedCoins.Any())
				{
					Logger.LogInfo($"Enqueued {Money.Satoshis(enqueuedCoins.Sum(x => x.Amount)).ToString(false, true)} BTC, {enqueuedCoins.Count()} coins with smallest anonset {enqueuedCoins.Min(x => x.AnonymitySet)} and largest anonset {enqueuedCoins.Max(x => x.AnonymitySet)}.");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}
	}
}
