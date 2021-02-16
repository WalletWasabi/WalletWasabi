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
			Money minRegistrableAmount,
			Money maxRegistrableAmount,
			uint minRegistrableWeight,
			uint maxRegistrableWeight,
			WasabiRandom random)
		{
			Network = network;
			MaxInputCountByAlice = maxInputCountByAlice;
			MinRegistrableAmount = minRegistrableAmount;
			MaxRegistrableAmount = maxRegistrableAmount;
			MinRegistrableWeight = minRegistrableWeight;
			MaxRegistrableWeight = maxRegistrableWeight;

			Random = random;
			AmountCredentialIssuer = new(new CredentialIssuerSecretKey(Random), 2, random, MaxRegistrableAmount);
			WeightCredentialIssuer = new(new CredentialIssuerSecretKey(Random), 2, random, MaxRegistrableWeight);
			AmountCredentialIssuerParameters = AmountCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();
			WeightCredentialIssuerParameters = WeightCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();

			Hash = new uint256(HashHelpers.GenerateSha256Hash($"{Id}{MaxInputCountByAlice}{MinRegistrableAmount}{MaxRegistrableAmount}{MinRegistrableWeight}{MaxRegistrableWeight}{AmountCredentialIssuerParameters}{WeightCredentialIssuerParameters}"));
		}

		public Round(Round blameOf)
			: this(
				blameOf.Network,
				blameOf.MaxInputCountByAlice,
				blameOf.MinRegistrableAmount,
				blameOf.MaxRegistrableAmount,
				blameOf.MinRegistrableWeight,
				blameOf.MaxRegistrableWeight,
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
		public Money MinRegistrableAmount { get; }
		public Money MaxRegistrableAmount { get; }
		public uint MinRegistrableWeight { get; }
		public uint MaxRegistrableWeight { get; }
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
			var inputWeightSum = 0;
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

				if (coin.TxOut.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
				{
					// Convert conservative P2WPKH size in virtual bytes to weight units.
					inputWeightSum += Constants.P2wpkhInputVirtualSize * 4;
				}
				else
				{
					throw new NotImplementedException($"Wight estimation isn't implemented for provided script type.");
				}
			}

			if (inputValueSum < MinRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughFunds);
			}
			if (inputValueSum > MaxRegistrableAmount)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.TooMuchFunds);
			}

			if (inputWeightSum < MinRegistrableWeight)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.NotEnoughWeight);
			}
			if (inputWeightSum > MaxRegistrableWeight)
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
				Alices.Add(alice);
			}

			return new(alice.Id, amountCredentialResponse, weightCredentialResponse);
		}
	}
}
