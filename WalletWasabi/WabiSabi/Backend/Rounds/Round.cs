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
		public Round()
		{
			Hash = new uint256(HashHelpers.GenerateSha256Hash($"{Id}"));
		}

		public Guid Id { get; } = Guid.NewGuid();
		public Phase Phase { get; set; } = Phase.InputRegistration;
		public uint256 Hash { get; }
	}
}
