using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class Round
	{
		public Round(
			uint maxInputCountByAlice,
			Money minRegistrableAmount,
			Money maxRegistrableAmount,
			uint minRegistrableWeight,
			uint maxRegistrableWeight)
		{
			MaxInputCountByAlice = maxInputCountByAlice;
			MinRegistrableAmount = minRegistrableAmount;
			MaxRegistrableAmount = maxRegistrableAmount;
			MinRegistrableWeight = minRegistrableWeight;
			MaxRegistrableWeight = maxRegistrableWeight;

			Hash = new uint256(HashHelpers.GenerateSha256Hash($"{Id}{MaxInputCountByAlice}{MinRegistrableAmount}{MaxRegistrableAmount}{MinRegistrableWeight}{MaxRegistrableWeight}"));
		}

		public Round(Round blameOf)
			: this(
				 blameOf.MaxInputCountByAlice,
				 blameOf.MinRegistrableAmount,
				 blameOf.MaxRegistrableAmount,
				 blameOf.MinRegistrableWeight,
				 blameOf.MaxRegistrableWeight)
		{
			BlameOf = blameOf;
			BlameWhitelist = blameOf.Alices.Select(x => x.OutPoint).ToHashSet();
		}

		public Guid Id { get; } = Guid.NewGuid();
		public Phase Phase { get; set; } = Phase.InputRegistration;
		public uint256 Hash { get; }
		public uint MaxInputCountByAlice { get; }
		public Money MinRegistrableAmount { get; }
		public Money MaxRegistrableAmount { get; }
		public uint MinRegistrableWeight { get; }
		public uint MaxRegistrableWeight { get; }
		public List<Alice> Alices { get; } = new();
		public Round? BlameOf { get; } = null;
		public bool IsBlameRound => BlameOf is not null;
		public ISet<OutPoint> BlameWhitelist { get; } = new HashSet<OutPoint>();

		public bool TryGetAlice(Guid aliceId, [NotNullWhen(true)] out Alice? alice)
		{
			alice = Alices.FirstOrDefault(x => x.Id == aliceId);
			return alice is not null;
		}
	}
}
