using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class AliceClient
	{
		public AliceClient(uint256 roundId, ArenaClient arenaClient, Coin coin, FeeRate feeRate, BitcoinSecret bitcoinSecret)
		{
			AliceId = Alice.CalculateHash(coin, bitcoinSecret, roundId);
			RoundId = roundId;
			ArenaClient = arenaClient;
			Coin = coin;
			FeeRate = feeRate;
			BitcoinSecret = bitcoinSecret;
		}

		public uint256 AliceId { get; }
		public uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }
		private Coin Coin { get; }
		private FeeRate FeeRate { get; }
		private BitcoinSecret BitcoinSecret { get; }

		public async Task RegisterInputAsync()
		{
			uint256 aliceId = await ArenaClient.RegisterInputAsync(Coin.Amount, Coin.Outpoint, BitcoinSecret.PrivateKey, RoundId).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({aliceId}): Registered an input.");
		}

		public async Task ConfirmConnectionAsync(TimeSpan confirmInterval, CancellationToken token)
		{
			while (!await ConfirmConnectionAsync().ConfigureAwait(false))
			{
				await Task.Delay(confirmInterval, token).ConfigureAwait(false);
			}
		}

		private async Task<bool> ConfirmConnectionAsync()
		{
			var inputVsize = Constants.P2wpkhInputVirtualSize;
			var inputRemainingVsizes = new[] { ProtocolConstants.MaxVsizePerAlice - inputVsize };

			var amountCredentials = ArenaClient.AmountCredentialClient.Credentials;

			var totalFeeToPay = FeeRate.GetFee(Coin.ScriptPubKey.EstimateInputVsize());
			var totalAmount = Coin.Amount;
			var effectiveAmount = totalAmount - totalFeeToPay;

			if (effectiveAmount <= Money.Zero)
			{
				throw new InvalidOperationException($"Round({ RoundId }), Alice({ AliceId}): Not enough funds to pay for the fees.");
			}

			var amountsToRequest = new[] { effectiveAmount };

			return await ArenaClient
				.ConfirmConnectionAsync(
					RoundId,
					AliceId,
					inputRemainingVsizes,
					amountCredentials.ZeroValue.Take(ProtocolConstants.CredentialNumber),
					amountsToRequest)
				.ConfigureAwait(false);
		}

		public async Task RemoveInputAsync()
		{
			await ArenaClient.RemoveInputAsync(RoundId, AliceId).ConfigureAwait(false);
			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Inputs removed.");
		}

		public async Task SignTransactionAsync(BitcoinSecret bitcoinSecret, Transaction unsignedCoinJoin)
		{
			await ArenaClient.SignTransactionAsync(RoundId, Coin, bitcoinSecret, unsignedCoinJoin).ConfigureAwait(false);

			Logger.LogInfo($"Round ({RoundId}), Alice ({AliceId}): Posted a signature.");
		}
	}
}
