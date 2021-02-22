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
		public byte[] UnsignedTxSecret { get; }
		public Transaction Coinjoin { get; }
		public RoundParameters RoundParameters { get; }

		public int RemoveAlices(Predicate<Alice> match)
		{
			if (Phase != Phase.InputRegistration)
			{
				throw new WabiSabiProtocolException(WabiSabiProtocolErrorCode.WrongPhase);
			}
			return Alices.RemoveAll(match);
		}
	}
}
