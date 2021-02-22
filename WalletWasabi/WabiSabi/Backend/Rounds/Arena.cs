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

		protected override Task ActionAsync(CancellationToken cancel)
		{
			lock (Lock)
			{
				return Task.CompletedTask;
			}
		}

		public InputsRegistrationResponse RegisterInput(
			Guid roundId,
			IDictionary<Coin, byte[]> coinRoundSignaturePairs,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroWeightCredentialRequests)
		{
			lock (Lock)
			{
				if (!Rounds.TryGetValue(roundId, out var round))
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

				if (round.Phase != Phase.InputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}

				var amountCredentialResponse = round.AmountCredentialIssuer.HandleRequest(zeroAmountCredentialRequests);
				var weightCredentialResponse = round.WeightCredentialIssuer.HandleRequest(zeroWeightCredentialRequests);

				alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeout);
				foreach (var otherRound in Rounds.Values)
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

				round.Alices.Add(alice);

				return new(alice.Id, amountCredentialResponse, weightCredentialResponse);
			}
		}

		public void RemoveInput(InputsRemovalRequest request)
		{
			lock (Lock)
			{
				if (!Rounds.TryGetValue(request.RoundId, out var round))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}
				round.RemoveAlices(x => x.Id == request.AliceId);
			}
		}

		public ConnectionConfirmationResponse ConfirmConnection(ConnectionConfirmationRequest request)
		{
			lock (Lock)
			{
				if (!Rounds.TryGetValue(request.RoundId, out var round))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}

				var alice = round.Alices.FirstOrDefault(x => x.Id == request.AliceId);
				if (alice is null)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound);
				}

				var amountZeroCredentialResponse = round.AmountCredentialIssuer.HandleRequest(request.ZeroAmountCredentialRequests);
				var weightZeroCredentialResponse = round.WeightCredentialIssuer.HandleRequest(request.ZeroWeightCredentialRequests);

				var realAmountCredentialRequests = request.RealAmountCredentialRequests;
				var realWeightCredentialRequests = request.RealWeightCredentialRequests;

				if (realWeightCredentialRequests.Delta != alice.CalculateRemainingWeightCredentials(round.RegistrableWeightCredentials))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials);
				}
				if (realAmountCredentialRequests.Delta != alice.CalculateRemainingAmountCredentials(round.FeeRate))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedAmountCredentials);
				}

				if (round.Phase == Phase.InputRegistration)
				{
					alice.SetDeadlineRelativeTo(round.ConnectionConfirmationTimeout);
					return new(
						amountZeroCredentialResponse,
						weightZeroCredentialResponse);
				}
				else if (round.Phase == Phase.ConnectionConfirmation)
				{
					var amountRealCredentialResponse = round.AmountCredentialIssuer.HandleRequest(realAmountCredentialRequests);
					var weightRealCredentialResponse = round.WeightCredentialIssuer.HandleRequest(realWeightCredentialRequests);

					return new(
						amountZeroCredentialResponse,
						weightZeroCredentialResponse,
						amountRealCredentialResponse,
						weightRealCredentialResponse);
				}
				else
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
			}
		}

		public OutputRegistrationResponse RegisterOutput(OutputRegistrationRequest request)
		{
			lock (Lock)
			{
				if (!Rounds.TryGetValue(request.RoundId, out var round))
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

				if (round.Phase != Phase.OutputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}

				var amountCredentialResponse = round.AmountCredentialIssuer.HandleRequest(request.AmountCredentialRequests);
				var weightCredentialResponse = round.WeightCredentialIssuer.HandleRequest(weightCredentialRequests);

				round.Bobs.Add(bob);

				return new(
					round.UnsignedTxSecret,
					amountCredentialResponse,
					weightCredentialResponse);
			}
		}

		public void SignTransaction(TransactionSignaturesRequest request)
		{
			lock (Lock)
			{
				if (!Rounds.TryGetValue(request.RoundId, out var round))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.RoundNotFound);
				}

				if (round.Phase != Phase.TransactionSigning)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
				foreach (var inputWitnessPair in request.InputWitnessPairs)
				{
					var index = (int)inputWitnessPair.InputIndex;
					var witness = inputWitnessPair.Witness;

					// If input is already signed, don't bother.
					if (round.Coinjoin.Inputs[index].HasWitScript())
					{
						continue;
					}

					// Verify witness.
					// 1. Copy UnsignedCoinJoin.
					Transaction cjCopy = Transaction.Parse(round.Coinjoin.ToHex(), Network);

					// 2. Sign the copy.
					cjCopy.Inputs[index].WitScript = witness;

					// 3. Convert the current input to IndexedTxIn.
					IndexedTxIn currentIndexedInput = cjCopy.Inputs.AsIndexedInputs().Skip(index).First();

					// 4. Find the corresponding registered input.
					Coin registeredCoin = round.Alices.SelectMany(x => x.Coins).Single(x => x.Outpoint == cjCopy.Inputs[index].PrevOut);

					// 5. Verify if currentIndexedInput is correctly signed, if not, return the specific error.
					if (!currentIndexedInput.VerifyScript(registeredCoin, out ScriptError error))
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongCoinjoinSignature);
					}

					// Finally add it to our CJ.
					round.Coinjoin.Inputs[index].WitScript = witness;
				}
			}
		}
	}
}
