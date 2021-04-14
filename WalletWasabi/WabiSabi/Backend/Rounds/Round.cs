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

			AmountCredentialIssuer = new(new(Random), 2, Random, MaxRegistrableAmount);
			VsizeCredentialIssuer = new(new(Random), 2, Random, PerAliceVsizeAllocation);
			AmountCredentialIssuerParameters = AmountCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();
			VsizeCredentialIssuerParameters = VsizeCredentialIssuer.CredentialIssuerSecretKey.ComputeCredentialIssuerParameters();

			Coinjoin = Transaction.Create(Network);

			Hash = new(HashHelpers.GenerateSha256Hash($"{Id}{MaxInputCountByAlice}{MinRegistrableAmount}{MaxRegistrableAmount}{PerAliceVsizeAllocation}{AmountCredentialIssuerParameters}{VsizeCredentialIssuerParameters}{FeeRate.SatoshiPerByte}"));
		}

		public uint256 Hash { get; }
		public Network Network => RoundParameters.Network;
		public uint MaxInputCountByAlice => RoundParameters.MaxInputCountByAlice;
		public Money MinRegistrableAmount => RoundParameters.MinRegistrableAmount;
		public Money MaxRegistrableAmount => RoundParameters.MaxRegistrableAmount;
		public uint PerAliceVsizeAllocation => RoundParameters.PerAliceVsizeAllocation;
		public FeeRate FeeRate => RoundParameters.FeeRate;
		public WasabiRandom Random => RoundParameters.Random;
		public CredentialIssuer AmountCredentialIssuer { get; }
		public CredentialIssuer VsizeCredentialIssuer { get; }
		public CredentialIssuerParameters AmountCredentialIssuerParameters { get; }
		public CredentialIssuerParameters VsizeCredentialIssuerParameters { get; }
		public Guid Id { get; } = Guid.NewGuid();
		public List<Alice> Alices { get; } = new();
		public int InputCount => Alices.Sum(x => x.Coins.Count());
		public List<Bob> Bobs { get; } = new();

		public Round? BlameOf => RoundParameters.BlameOf;
		public bool IsBlameRound => RoundParameters.IsBlameRound;
		public ISet<OutPoint> BlameWhitelist => RoundParameters.BlameWhitelist;

		public TimeSpan ConnectionConfirmationTimeout => RoundParameters.ConnectionConfirmationTimeout;
		public TimeSpan OutputRegistrationTimeout => RoundParameters.OutputRegistrationTimeout;
		public TimeSpan TransactionSigningTimeout => RoundParameters.TransactionSigningTimeout;

		public Transaction Coinjoin { get; }
		private RoundParameters RoundParameters { get; }
		public Phase Phase { get; private set; } = Phase.InputRegistration;
		public DateTimeOffset InputRegistrationStart { get; } = DateTimeOffset.UtcNow;
		public DateTimeOffset ConnectionConfirmationStart { get; private set; }
		public DateTimeOffset OutputRegistrationStart { get; private set; }
		public DateTimeOffset TransactionSigningStart { get; private set; }
		public DateTimeOffset TransactionBroadcastingStart { get; private set; }
		public int InitialInputVsizeAllocation { get; set; } = 99954; // TODO compute as CoinjoinState.Parameters.MaxWeight - CoinjoinState.Parameters.SharedOverhead, mutable for testing until then
		public int RemainingInputVsizeAllocation => InitialInputVsizeAllocation - Alices.Count * (int)PerAliceVsizeAllocation;

		public void SetPhase(Phase phase)
		{
			if (!Enum.IsDefined<Phase>(phase))
			{
				throw new ArgumentException($"Invalid phase {phase}. This is a bug.", nameof(phase));
			}

			this.LogInfo($"Phase changed: {Phase} -> {phase}");
			Phase = phase;

			if (phase == Phase.ConnectionConfirmation)
			{
				ConnectionConfirmationStart = DateTimeOffset.UtcNow;
			}
			else if (phase == Phase.OutputRegistration)
			{
				OutputRegistrationStart = DateTimeOffset.UtcNow;
			}
			else if (phase == Phase.TransactionSigning)
			{
				TransactionSigningStart = DateTimeOffset.UtcNow;
			}
			else if (phase == Phase.TransactionBroadcasting)
			{
				TransactionBroadcastingStart = DateTimeOffset.UtcNow;
			}
		}

		public bool IsInputRegistrationEnded(uint maxInputCount, TimeSpan inputRegistrationTimeout)
		{
			if (Phase > Phase.InputRegistration)
			{
				return true;
			}

			if (IsBlameRound)
			{
				if (BlameWhitelist.Count <= InputCount)
				{
					return true;
				}
			}
			else if (InputCount >= maxInputCount)
			{
				return true;
			}

			if (InputRegistrationStart + inputRegistrationTimeout < DateTimeOffset.UtcNow)
			{
				return true;
			}

			return false;
		}
	}
}
