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
using WalletWasabi.WabiSabi.Backend.PostRequests;
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
				// Remove timed out alices.
				foreach (var round in Rounds.Values.Where(x => x.Phase == Phase.InputRegistration))
				{
					var removedAliceCount = round.RemoveAlices(x => x.Deadline < DateTimeOffset.UtcNow);
					if (removedAliceCount > 0)
					{
						Logger.LogInfo($"{removedAliceCount} alices timed out and removed.");
					}
				}

				return Task.CompletedTask;
			}
		}

		public InputsRegistrationResponse RegisterInput(
			Guid roundId,
			Dictionary<Coin, byte[]> coinRoundSignaturePairs,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroWeightCredentialRequests)
		{
			lock (Lock)
			{
				return InputRegistrationHandler.RegisterInput(
					roundId,
					coinRoundSignaturePairs,
					zeroAmountCredentialRequests,
					zeroWeightCredentialRequests,
					Rounds,
					Network);
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
