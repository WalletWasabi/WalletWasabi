using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Arena : PeriodicRunner
	{
		public Arena(TimeSpan period, Network network) : base(period)
		{
			Network = network;
		}

		public Dictionary<Guid, Round> Rounds { get; } = new();
		private object Lock { get; } = new();
		public Network Network { get; }

		public bool TryGetRound(Guid roundId, [NotNullWhen(true)] out Round? round)
		{
			lock (Lock)
			{
				return Rounds.TryGetValue(roundId, out round);
			}
		}

		protected override Task ActionAsync(CancellationToken cancel)
		{
			return Task.CompletedTask;
		}

		public InputsRegistrationResponse RegisterInput(
			Guid roundId,
			IDictionary<Coin, byte[]> coinRoundSignaturePairs,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroWeightCredentialRequests)
		{
			if (!TryGetRound(roundId, out var round))
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
				var address = (BitcoinWitPubKeyAddress)coin.TxOut.ScriptPubKey.GetDestinationAddress(Network);
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

			var resp = round.RegisterAlice(
				alice,
				zeroAmountCredentialRequests,
				zeroWeightCredentialRequests);

			lock (Lock)
			{
				foreach (var otherRound in Rounds.Where(x => x.Key != round.Id).Select(x => x.Value))
				{
					foreach (var op in alice.Coins.Select(x => x.Outpoint))
					{
						try
						{
							if (otherRound.RemoveAlices(x => x.Coins.Select(x => x.Outpoint).Contains(op)) > 0)
							{
								Logger.LogInfo("Cross round updated Alice registration.");
							}
						}
						catch (WabiSabiProtocolException ex) when (ex.ErrorCode == WabiSabiProtocolErrorCode.WrongPhase)
						{
							throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceAlreadyRegistered);
						}
					}
				}
			}

			return resp;
		}

		public void RemoveInput(InputsRemovalRequest request)
		{
			if (!TryGetRound(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}
			round.RemoveAlices(x => x.Id == request.AliceId);
		}

		public ConnectionConfirmationResponse ConfirmConnection(ConnectionConfirmationRequest request)
		{
			if (!TryGetRound(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}

			return round.ConfirmAlice(request);
		}

		public OutputRegistrationResponse RegisterOutput(OutputRegistrationRequest request)
		{
			if (!TryGetRound(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}

			var credentialAmount = -request.AmountCredentialRequests.Delta;

			var bob = new Bob(request.Script, credentialAmount);

			var outputValue = bob.CalculateOutputAmount(round.FeeRate);
			if (outputValue < round.MinRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
			}
			if (outputValue > round.MaxRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
			}

			var weightCredentialRequests = request.WeightCredentialRequests;
			if (-weightCredentialRequests.Delta != bob.CalculateWeight())
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials);
			}

			return round.RegisterBob(bob, request.AmountCredentialRequests, weightCredentialRequests);
		}

		public void SignTransaction(TransactionSignaturesRequest request)
		{
			if (!TryGetRound(request.RoundId, out var round))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
			}
			round.SubmitTransactionSignatures(request.InputWitnessPairs);
		}
	}
}
