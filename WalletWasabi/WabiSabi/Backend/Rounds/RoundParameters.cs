using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Randomness;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class RoundParameters
	{
		public RoundParameters(
			WabiSabiConfig wabiSabiConfig,
			Network network,
			WasabiRandom random,
			FeeRate feeRate)
		{
			Network = network;
			Random = random;
			FeeRate = feeRate;
			MaxInputCountByAlice = wabiSabiConfig.MaxInputCountByAlice;
			MinRegistrableAmount = wabiSabiConfig.MinRegistrableAmount;
			MaxRegistrableAmount = wabiSabiConfig.MaxRegistrableAmount;
			RegistrableWeightCredentials = wabiSabiConfig.RegistrableWeightCredentials;
		}

		public WasabiRandom Random { get; }
		public FeeRate FeeRate { get; }
		public Network Network { get; }
		public uint MaxInputCountByAlice { get; }
		public Money MinRegistrableAmount { get; }
		public Money MaxRegistrableAmount { get; }
		public uint RegistrableWeightCredentials { get; }
	}
}
