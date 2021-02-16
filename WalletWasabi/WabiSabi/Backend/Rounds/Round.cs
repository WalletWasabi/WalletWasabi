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
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Round
	{
		public Round(
			Network network,
			uint maxInputCountByAlice,
			Money minRegistrableAmountByAlice,
			Money maxRegistrableAmountByAlice,
			uint registrableWeightCredentials,
			TimeSpan connectionConfirmationTimeout,
			TimeSpan outputRegistrationTimeout,
			TimeSpan transactionSigningTimeout,
			WasabiRandom random)
		{
			Network = network;
			MaxInputCountByAlice = maxInputCountByAlice;
			MinRegistrableAmountByAlice = minRegistrableAmountByAlice;
			MaxRegistrableAmountByAlice = maxRegistrableAmountByAlice;
			RegistrableWeightCredentials = registrableWeightCredentials;

			ConnectionConfirmationTimeout = connectionConfirmationTimeout;
			OutputRegistrationTimeout = outputRegistrationTimeout;
			TransactionSigningTimeout = transactionSigningTimeout;

			Random = random;
			AmountCredentialIssuer = new(new(Random), 2, random, MaxRegistrableAmountByAlice);
			WeightCredentialIssuer = new(new(Random), 2, random, RegistrableWeightCredentials);
			AmountCredentialIssuerParameters = AmountCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();
			WeightCredentialIssuerParameters = WeightCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();

			Hash = new(HashHelpers.GenerateSha256Hash($"{Id}{MaxInputCountByAlice}{MinRegistrableAmountByAlice}{MaxRegistrableAmountByAlice}{RegistrableWeightCredentials}{AmountCredentialIssuerParameters}{WeightCredentialIssuerParameters}"));
		}

		public Round(Round blameOf)
			: this(
				blameOf.Network,
				blameOf.MaxInputCountByAlice,
				blameOf.MinRegistrableAmountByAlice,
				blameOf.MaxRegistrableAmountByAlice,
				blameOf.RegistrableWeightCredentials,
				blameOf.ConnectionConfirmationTimeout,
				blameOf.OutputRegistrationTimeout,
				blameOf.TransactionSigningTimeout,
				blameOf.Random)
		{
			BlameOf = blameOf;
			BlameWhitelist = blameOf
				.Alices
				.SelectMany(x => x.Coins)
				.Select(x => x.Outpoint)
				.ToHashSet();
		}

		public uint256 Hash { get; }
		public Network Network { get; }
		public uint MaxInputCountByAlice { get; }
		public Money MinRegistrableAmountByAlice { get; }
		public Money MaxRegistrableAmountByAlice { get; }
		public uint RegistrableWeightCredentials { get; }
		public TimeSpan ConnectionConfirmationTimeout { get; }
		public TimeSpan OutputRegistrationTimeout { get; }
		public TimeSpan TransactionSigningTimeout { get; }
		public WasabiRandom Random { get; }
		public CredentialIssuer AmountCredentialIssuer { get; }
		public CredentialIssuer WeightCredentialIssuer { get; }
		public CredentialIssuerParameters AmountCredentialIssuerParameters { get; }
		public CredentialIssuerParameters WeightCredentialIssuerParameters { get; }
		public Guid Id { get; } = Guid.NewGuid();
		public Phase Phase { get; set; } = Phase.InputRegistration;
		public List<Alice> Alices { get; } = new();
		public Round? BlameOf { get; } = null;
		public bool IsBlameRound => BlameOf is not null;
		public ISet<OutPoint> BlameWhitelist { get; } = new HashSet<OutPoint>();
		private object Lock { get; } = new();

		public bool TryGetAlice(Guid aliceId, [NotNullWhen(true)] out Alice? alice)
		{
			alice = Alices.FirstOrDefault(x => x.Id == aliceId);
			return alice is not null;
		}

		public InputsRegistrationResponse RegisterAlice(
			Alice alice,
			ZeroCredentialsRequest zeroAmountCredentialRequests,
			ZeroCredentialsRequest zeroWeightCredentialRequests)
		{
			var coins = alice.Coins;
			if (MaxInputCountByAlice < coins.Count())
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooManyInputs);
			}
			if (IsBlameRound && coins.Select(x => x.Outpoint).Any(x => !BlameWhitelist.Contains(x)))
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.InputNotWhitelisted);
			}

			var inputValueSum = Money.Zero;
			var inputWeightSum = 0u;
			foreach (var coinRoundSignaturePair in alice.CoinRoundSignaturePairs)
			{
				var coin = coinRoundSignaturePair.Key;
				var signature = coinRoundSignaturePair.Value;
				var address = (BitcoinWitPubKeyAddress)coin.TxOut.ScriptPubKey.GetDestinationAddress(Network);
				if (!address.VerifyMessage(Hash, signature))
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongRoundSignature);
				}
				inputValueSum += coin.TxOut.Value;

				// Convert conservative P2WPKH size in virtual bytes to weight units.
				inputWeightSum += coin.TxOut.ScriptPubKey.EstimateSpendVsize() * 4;
			}

			if (inputValueSum < MinRegistrableAmountByAlice)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
			}
			if (inputValueSum > MaxRegistrableAmountByAlice)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
			}

			if (inputWeightSum > RegistrableWeightCredentials)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchWeight);
			}

			var amountCredentialResponse = AmountCredentialIssuer.HandleRequest(zeroAmountCredentialRequests);
			var weightCredentialResponse = WeightCredentialIssuer.HandleRequest(zeroWeightCredentialRequests);

			lock (Lock)
			{
				if (Phase != Phase.InputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
				alice.SetDeadlineRelativeTo(ConnectionConfirmationTimeout);
				Alices.Add(alice);
			}

			return new(alice.Id, amountCredentialResponse, weightCredentialResponse);
		}

		public ConnectionConfirmationResponse ConfirmAlice(Guid aliceId, ZeroCredentialsRequest zeroAmountCredentialRequests, RealCredentialsRequest realAmountCredentialRequests, ZeroCredentialsRequest zeroWeightCredentialRequests, RealCredentialsRequest realWeightCredentialRequests)
		{
			var amountZeroCredentialResponse = AmountCredentialIssuer.HandleRequest(zeroAmountCredentialRequests);
			var weightZeroCredentialResponse = WeightCredentialIssuer.HandleRequest(zeroWeightCredentialRequests);

			lock (Lock)
			{
				var alice = Alices.FirstOrDefault(x => x.Id == aliceId);
				if (alice is null)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.AliceNotFound);
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

					if (realWeightCredentialRequests.Delta != alice.CalculateRemainingWeightCredentials(RegistrableWeightCredentials))
					{
						throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials);
					}

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

		internal void RemoveAlice(Guid aliceId)
		{
			lock (Lock)
			{
				if (Phase != Phase.InputRegistration)
				{
					throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
				}
				Alices.RemoveAll(x => x.Id == aliceId);
			}
		}
	}
}
