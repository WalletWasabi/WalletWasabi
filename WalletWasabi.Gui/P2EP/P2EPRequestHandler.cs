using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.P2EP
{
	public class P2EPRequestHandler
	{
		public P2EPRequestHandler(Network network, WalletManager walletManager, int privacyLevelThreshold)
		{
			Network = network;
			WalletManager = walletManager;
			PrivacyLevelThreshold = privacyLevelThreshold;
		}

		public Network Network { get; }
		public WalletManager WalletManager { get; }
		public int PrivacyLevelThreshold { get; }

		public Task<string> HandleAsync(string body, CancellationToken cancellationToken)
		{
			NotificationHelpers.Notify("request received!!!!", "PAYJOIN", NotificationType.Information);
			if (!PSBT.TryParse(body, Network, out var psbt))
			{
				throw new P2EPException("What the heck are you trying to do?");
			}
			if (!psbt.IsAllFinalized())
			{
				throw new P2EPException("The PSBT should be finalized");
			}
			
			var toUse = WalletManager.GetWallets()
				.Where( x => x.State == WalletState.Started && !x.KeyManager.IsWatchOnly && !x.KeyManager.IsHardwareWallet )
				.SelectMany( wallet => wallet.Coins.Select(coin => new { wallet.KeyManager, coin }))
				.Where(x => x.coin.AnonymitySet >= PrivacyLevelThreshold && !x.coin.Unavailable)
				.OrderBy(x => x.coin.IsBanned)
				.ThenBy(x => x.coin.Confirmed)
				.ThenBy(x => x.coin.Height)
				.First();

			var originalFeeRate = psbt.GetEstimatedFeeRate();
			var paymentTx = psbt.ExtractTransaction();
			foreach (var input in paymentTx.Inputs)
			{
				input.WitScript = WitScript.Empty;
			}
			var serverCoinKey = toUse.KeyManager.GetSecrets("", toUse.coin.ScriptPubKey).First();
			var serverCoin = toUse.coin.GetCoin();
			paymentTx.Inputs.Add(serverCoin.Outpoint);
			var paymentOutput = paymentTx.Outputs.First();
			var inputSizeInVBytes = (int)Math.Ceiling(((3 * Constants.P2wpkhInputSizeInBytes) + Constants.P2pkhInputSizeInBytes) / 4m);
			paymentOutput.Value += (Money)serverCoin.Amount - originalFeeRate.GetFee(inputSizeInVBytes);
			var newPsbt = PSBT.FromTransaction(paymentTx, Network.Main);
			var serverCoinToSign = newPsbt.Inputs.FindIndexedInput(serverCoin.Outpoint);
			serverCoinToSign.UpdateFromCoin(serverCoin);
			serverCoinToSign.Sign(serverCoinKey.PrivateKey);
			serverCoinToSign.FinalizeInput();

			return Task.FromResult(newPsbt.ToHex());
		}
	}
}
