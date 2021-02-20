using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Round
	{
		public Round(RoundParameters roundParameters)
		{
			RoundParameters = roundParameters;
			UnsignedTxSecret = Random.GetBytes(64);

			AmountCredentialIssuer = new(new(Random), 2, Random, MaxRegistrableAmount);
			WeightCredentialIssuer = new(new(Random), 2, Random, RegistrableWeightCredentials);
			AmountCredentialIssuerParameters = AmountCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();
			WeightCredentialIssuerParameters = WeightCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();

			Coinjoin = Transaction.Create(Network);

			Hash = new(HashHelpers.GenerateSha256Hash($"{Id}{MaxInputCountByAlice}{MinRegistrableAmount}{MaxRegistrableAmount}{RegistrableWeightCredentials}{AmountCredentialIssuerParameters}{WeightCredentialIssuerParameters}{FeeRate.SatoshiPerByte}"));
		}

		public Round(Round blameOf) : this(blameOf.RoundParameters)
		{
			BlameOf = blameOf;
			BlameWhitelist = blameOf
				.Alices
				.SelectMany(x => x.Coins)
				.Select(x => x.Outpoint)
				.ToHashSet();
		}

		public uint256 Hash { get; }
		public Network Network => RoundParameters.Network;
		public uint MaxInputCountByAlice => RoundParameters.MaxInputCountByAlice;
		public Money MinRegistrableAmount => RoundParameters.MinRegistrableAmount;
		public Money MaxRegistrableAmount => RoundParameters.MaxRegistrableAmount;
		public uint RegistrableWeightCredentials => RoundParameters.RegistrableWeightCredentials;
		public TimeSpan ConnectionConfirmationTimeout => RoundParameters.ConnectionConfirmationTimeout;
		public TimeSpan OutputRegistrationTimeout => RoundParameters.OutputRegistrationTimeout;
		public TimeSpan TransactionSigningTimeout => RoundParameters.TransactionSigningTimeout;
		public FeeRate FeeRate => RoundParameters.FeeRate;
		public WasabiRandom Random => RoundParameters.Random;
		public CredentialIssuer AmountCredentialIssuer { get; }
		public CredentialIssuer WeightCredentialIssuer { get; }
		public CredentialIssuerParameters AmountCredentialIssuerParameters { get; }
		public CredentialIssuerParameters WeightCredentialIssuerParameters { get; }
		public Guid Id { get; } = Guid.NewGuid();
		public Phase Phase { get; set; } = Phase.InputRegistration;
		public List<Alice> Alices { get; } = new();
		public List<Bob> Bobs { get; } = new();
		public Round? BlameOf { get; } = null;
		public bool IsBlameRound => BlameOf is not null;
		public ISet<OutPoint> BlameWhitelist { get; } = new HashSet<OutPoint>();
		private object Lock { get; } = new();
		public byte[] UnsignedTxSecret { get; }
		public Transaction Coinjoin { get; }
		public RoundParameters RoundParameters { get; }

		public InputsRegistrationResponse RegisterAlice(
			Alice alice,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroWeightCredentialRequests)
		{
			lock (Lock)
			{
				if (Phase != Phase.InputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}

				var amountCredentialResponse = AmountCredentialIssuer.HandleRequest(zeroAmountCredentialRequests);
				var weightCredentialResponse = WeightCredentialIssuer.HandleRequest(zeroWeightCredentialRequests);

				alice.SetDeadlineRelativeTo(ConnectionConfirmationTimeout);
				foreach (var op in alice.Coins.Select(x => x.Outpoint))
				{
					if (RemoveAlicesNoLock(x => x.Coins.Select(x => x.Outpoint).Contains(op)) > 0)
					{
						Logger.LogInfo("Updated Alice registration.");
					}
				}
				Alices.Add(alice);

				return new(alice.Id, amountCredentialResponse, weightCredentialResponse);
			}
		}

		public int RemoveAlices(Predicate<Alice> match)
		{
			lock (Lock)
			{
				return RemoveAlicesNoLock(match);
			}
		}

		private int RemoveAlicesNoLock(Predicate<Alice> match)
		{
			if (Phase != Phase.InputRegistration)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
			}
			return Alices.RemoveAll(match);
		}

		public ConnectionConfirmationResponse ConfirmAlice(ConnectionConfirmationRequest request)
		{
			lock (Lock)
			{
				var alice = Alices.FirstOrDefault(x => x.Id == request.AliceId);
				if (alice is null)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound);
				}

				var amountZeroCredentialResponse = AmountCredentialIssuer.HandleRequest(request.ZeroAmountCredentialRequests);
				var weightZeroCredentialResponse = WeightCredentialIssuer.HandleRequest(request.ZeroWeightCredentialRequests);

				var realAmountCredentialRequests = request.RealAmountCredentialRequests;
				var realWeightCredentialRequests = request.RealWeightCredentialRequests;

				if (realWeightCredentialRequests.Delta != alice.CalculateRemainingWeightCredentials(RegistrableWeightCredentials))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials);
				}
				if (realAmountCredentialRequests.Delta != alice.CalculateRemainingAmountCredentials(FeeRate))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedAmountCredentials);
				}

				if (Phase == Phase.InputRegistration)
				{
					alice.SetDeadlineRelativeTo(ConnectionConfirmationTimeout);
					return new(
						amountZeroCredentialResponse,
						weightZeroCredentialResponse);
				}
				else if (Phase == Phase.ConnectionConfirmation)
				{
					var amountRealCredentialResponse = AmountCredentialIssuer.HandleRequest(realAmountCredentialRequests);
					var weightRealCredentialResponse = WeightCredentialIssuer.HandleRequest(realWeightCredentialRequests);

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

		public OutputRegistrationResponse RegisterBob(Bob bob, RealCredentialsRequest amountCredentialRequests, RealCredentialsRequest weightCredentialRequests)
		{
			lock (Lock)
			{
				if (Phase != Phase.OutputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}

				var amountCredentialResponse = AmountCredentialIssuer.HandleRequest(amountCredentialRequests);
				var weightCredentialResponse = WeightCredentialIssuer.HandleRequest(weightCredentialRequests);

				Bobs.Add(bob);

				return new(
					UnsignedTxSecret,
					amountCredentialResponse,
					weightCredentialResponse);
			}
		}

		public void SubmitTransactionSignatures(IEnumerable<InputWitnessPair> inputWitnessPairs)
		{
			lock (Lock)
			{
				if (Phase != Phase.TransactionSigning)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
				foreach (var inputWitnessPair in inputWitnessPairs)
				{
					var index = (int)inputWitnessPair.InputIndex;
					var witness = inputWitnessPair.Witness;

					// If input is already signed, don't bother.
					if (Coinjoin.Inputs[index].HasWitScript())
					{
						continue;
					}

					// Verify witness.
					// 1. Copy UnsignedCoinJoin.
					Transaction cjCopy = Transaction.Parse(Coinjoin.ToHex(), Network);

					// 2. Sign the copy.
					cjCopy.Inputs[index].WitScript = witness;

					// 3. Convert the current input to IndexedTxIn.
					IndexedTxIn currentIndexedInput = cjCopy.Inputs.AsIndexedInputs().Skip(index).First();

					// 4. Find the corresponding registered input.
					Coin registeredCoin = Alices.SelectMany(x => x.Coins).Single(x => x.Outpoint == cjCopy.Inputs[index].PrevOut);

					// 5. Verify if currentIndexedInput is correctly signed, if not, return the specific error.
					if (!currentIndexedInput.VerifyScript(registeredCoin, out ScriptError error))
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongCoinjoinSignature);
					}

					// Finally add it to our CJ.
					Coinjoin.Inputs[index].WitScript = witness;
				}
			}
		}
	}
}
