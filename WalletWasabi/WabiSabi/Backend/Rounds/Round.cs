using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;

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

		public Guid Id { get; } = Guid.NewGuid();
		public Phase Phase { get; set; } = Phase.InputRegistration;
		public uint256 Hash { get; }
		public uint MaxInputCountByAlice { get; }
		public Money MinRegistrableAmount { get; }
		public Money MaxRegistrableAmount { get; }
		public uint MinRegistrableWeight { get; }
		public uint MaxRegistrableWeight { get; }
	}
}
