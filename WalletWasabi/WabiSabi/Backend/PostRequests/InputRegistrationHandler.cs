using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public static class InputRegistrationHandler
	{
		public static async Task<Dictionary<Coin, byte[]>> PreProcessAsync(
			InputsRegistrationRequest request,
			Prison prison,
			IRPCClient rpc,
			WabiSabiConfig config)
		{
			var inputRoundSignaturePairs = request.InputRoundSignaturePairs;
			var inputs = inputRoundSignaturePairs.Select(x => x.Input);

			int inputCount = inputs.Count();
			if (inputCount != inputs.Distinct().Count())
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NonUniqueInputs);
			}
			if (inputs.Any(x => prison.TryGet(x, out var inmate) && (!config.AllowNotedInputRegistration || inmate.Punishment != Punishment.Noted)))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputBanned);
			}

			Dictionary<Coin, byte[]> coinRoundSignaturePairs = new();
			foreach (var inputRoundSignaturePair in inputRoundSignaturePairs)
			{
				OutPoint input = inputRoundSignaturePair.Input;
				var txOutResponse = await rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true).ConfigureAwait(false);
				if (txOutResponse is null)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputSpent);
				}
				if (txOutResponse.Confirmations == 0)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputUnconfirmed);
				}
				if (txOutResponse.IsCoinBase && txOutResponse.Confirmations <= 100)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputImmature);
				}
				if (!txOutResponse.TxOut.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.ScriptNotAllowed);
				}

				coinRoundSignaturePairs.Add(new Coin(input, txOutResponse.TxOut), inputRoundSignaturePair.RoundSignature);
			}

			return coinRoundSignaturePairs;
		}

		public static InputsRegistrationResponse RegisterInput(
			Guid roundId,
			IDictionary<Coin, byte[]> coinRoundSignaturePairs,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroWeightCredentialRequests,
			IDictionary<Guid, Round> rounds,
			Network network)
		{
			if (!rounds.TryGetValue(roundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}

			var alice = new Alice(coinRoundSignaturePairs);

			var coins = alice.Coins;
			if (round.MaxInputCountByAlice < coins.Count())
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooManyInputs);
			}
			if (round.IsBlameRound && coins.Select(x => x.Outpoint).Any(x => !round.BlameWhitelist.Contains(x)))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputNotWhitelisted);
			}

			var inputValueSum = Money.Zero;
			var inputWeightSum = 0;
			foreach (var coinRoundSignaturePair in alice.CoinRoundSignaturePairs)
			{
				var coin = coinRoundSignaturePair.Key;
				var signature = coinRoundSignaturePair.Value;
				var address = (BitcoinWitPubKeyAddress)coin.TxOut.ScriptPubKey.GetDestinationAddress(network);
				if (!address.VerifyMessage(round.Hash, signature))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongRoundSignature);
				}
				inputValueSum += coin.TxOut.Value;

				// Convert conservative P2WPKH size in virtual bytes to weight units.
				inputWeightSum += coin.TxOut.ScriptPubKey.EstimateInputVsize() * 4;
			}

			if (inputValueSum < round.MinRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
			}
			if (inputValueSum > round.MaxRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
			}

			if (inputWeightSum > round.RegistrableWeightCredentials)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchWeight);
			}

			if (round.Phase != Phase.InputRegistration)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
			}

			var amountCredentialResponse = round.AmountCredentialIssuer.HandleRequest(zeroAmountCredentialRequests);
			var weightCredentialResponse = round.WeightCredentialIssuer.HandleRequest(zeroWeightCredentialRequests);

			alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeout);
			foreach (var otherRound in rounds.Values)
			{
				foreach (var op in alice.Coins.Select(x => x.Outpoint))
				{
					try
					{
						if (otherRound.RemoveAlices(x => x.Coins.Select(x => x.Outpoint).Contains(op)) > 0)
						{
							Logger.LogInfo("Updated Alice registration.");
						}
					}
					catch (WabiSabiProtocolException ex) when (ex.ErrorCode == WabiSabiProtocolErrorCode.WrongPhase)
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceAlreadyRegistered);
					}
				}
			}

			round.Alices.Add(alice);

			return new(alice.Id, amountCredentialResponse, weightCredentialResponse);
		}
	}
}
