using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public static class InputRegistrationHandler
	{
		public static async Task<Coin> OutpointToCoinAsync(
			InputRegistrationRequest request,
			Prison prison,
			IRPCClient rpc,
			WabiSabiConfig config)
		{
			OutPoint input = request.Input;

			if (prison.TryGet(input, out var inmate) && (!config.AllowNotedInputRegistration || inmate.Punishment != Punishment.Noted))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputBanned);
			}

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

			return new Coin(input, txOutResponse.TxOut);
		}

		public static InputRegistrationResponse RegisterInput(
			WabiSabiConfig config,
			Guid roundId,
			Coin coin,
			byte[] signature,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroVsizeCredentialRequests,
			IDictionary<Guid, Round> rounds,
			Network network)
		{
			if (!rounds.TryGetValue(roundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}

			if (round.IsInputRegistrationEnded(config.MaxInputCountByRound, config.GetInputRegistrationTimeout(round)))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
			}

			if (round.IsBlameRound && !round.BlameWhitelist.Contains(coin.Outpoint))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputNotWhitelisted);
			}

			// Compute but don't commit updated CoinJoin to round state, it will
			// be re-calculated on input confirmation. This is computed it here
			// for validation purposes.
			round.CoinjoinState.AssertConstruction().AddInput(coin);

			var coinJoinInputCommitmentData = new CoinJoinInputCommitmentData("CoinJoinCoordinatorIdentifier", round.Hash);
			if (!OwnershipProof.VerifyCoinJoinInputProof(signature, coin.TxOut.ScriptPubKey, coinJoinInputCommitmentData))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongRoundSignature);
			}

			var alice = new Alice(coin, signature);

			if (alice.TotalInputAmount < round.MinRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
			}
			if (alice.TotalInputAmount > round.MaxRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
			}

			if (alice.TotalInputVsize > round.PerAliceVsizeAllocation)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchVsize);
			}

			if (round.RemainingInputVsizeAllocation < round.PerAliceVsizeAllocation)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.VsizeQuotaExceeded);
			}
			var commitAmountCredentialResponse = round.AmountCredentialIssuer.PrepareResponse(zeroAmountCredentialRequests);
			var commitVsizeCredentialResponse = round.VsizeCredentialIssuer.PrepareResponse(zeroVsizeCredentialRequests);

			RemoveDuplicateAlices(rounds, alice);

			alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeout);
			round.Alices.Add(alice);

			return new(alice.Id,
				commitAmountCredentialResponse.Commit(),
				commitVsizeCredentialResponse.Commit());
		}

		private static void RemoveDuplicateAlices(IDictionary<Guid, Round> roundsWithId, Alice alice)
		{
			var rounds = roundsWithId.Values;
			if (rounds.Any(x => x.Phase != Phase.InputRegistration))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceAlreadyRegistered);
			}

			var aliceOutPoint = alice.Coin.Outpoint;
			var flattenTable = rounds.SelectMany(x => x.Alices.Select(y => (Round: x, Alice: y, Outpoint: y.Coin.Outpoint)));

			foreach (var (round, aliceInRound, _) in flattenTable.Where(x => x.Outpoint == aliceOutPoint).ToArray())
			{
				if (round.Alices.Remove(aliceInRound))
				{
					Logger.LogInfo("Updated Alice registration.");
				}
			}
		}
	}
}
