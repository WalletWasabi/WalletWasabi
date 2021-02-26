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
			FeeRate feeRate,
			Round? blameOf = null)
		{
			Network = network;
			Random = random;
			FeeRate = feeRate;

			MaxInputCountByAlice = wabiSabiConfig.MaxInputCountByAlice;
			MinRegistrableAmount = wabiSabiConfig.MinRegistrableAmount;
			MaxRegistrableAmount = wabiSabiConfig.MaxRegistrableAmount;
			RegistrableWeightCredentials = wabiSabiConfig.RegistrableWeightCredentials;

			BlameOf = blameOf;
			IsBlameRound = BlameOf is not null;
			BlameWhitelist = BlameOf
				?.Alices
				.SelectMany(x => x.Coins)
				.Select(x => x.Outpoint)
				.ToHashSet()
			?? new HashSet<OutPoint>();

			InputRegistrationTimeout = IsBlameRound ? wabiSabiConfig.BlameInputRegistrationTimeout : wabiSabiConfig.InputRegistrationTimeout;
			ConnectionConfirmationTimeout = wabiSabiConfig.ConnectionConfirmationTimeout;
			OutputRegistrationTimeout = wabiSabiConfig.OutputRegistrationTimeout;
			TransactionSigningTimeout = wabiSabiConfig.TransactionSigningTimeout;
		}

		public WasabiRandom Random { get; }
		public FeeRate FeeRate { get; }
		public Network Network { get; }
		public uint MaxInputCountByAlice { get; }
		public Money MinRegistrableAmount { get; }
		public Money MaxRegistrableAmount { get; }
		public uint RegistrableWeightCredentials { get; }
		public Round? BlameOf { get; }
		public bool IsBlameRound { get; }
		public ISet<OutPoint> BlameWhitelist { get; }
		public TimeSpan InputRegistrationTimeout { get; }
		public TimeSpan ConnectionConfirmationTimeout { get; }
		public TimeSpan OutputRegistrationTimeout { get; }
		public TimeSpan TransactionSigningTimeout { get; }
	}
}
