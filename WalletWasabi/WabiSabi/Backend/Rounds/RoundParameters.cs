using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Banning;

namespace WalletWasabi.WabiSabi.Backend.Rounds
{
	public class RoundParameters
	{
		public RoundParameters(
			WabiSabiConfig wabiSabiConfig,
			Network network,
			WasabiRandom random,
			FeeRate feeRate) : this(wabiSabiConfig, network, random, feeRate, null)
		{
		}

		private RoundParameters(
			WabiSabiConfig wabiSabiConfig,
			Network network,
			WasabiRandom random,
			FeeRate feeRate,
			Round? blameOf = null,
			Prison? prison = null)
		{
			Network = network;
			Random = random;
			FeeRate = feeRate;

			MaxInputCountByRound = wabiSabiConfig.MaxInputCountByRound;
			MinInputCountByRound = wabiSabiConfig.MinInputCountByRound;
			MinRegistrableAmount = wabiSabiConfig.MinRegistrableAmount;
			MaxRegistrableAmount = wabiSabiConfig.MaxRegistrableAmount;

			// Note that input registration timeouts can be modified runtime.
			StandardInputRegistrationTimeout = wabiSabiConfig.StandardInputRegistrationTimeout;
			ConnectionConfirmationTimeout = wabiSabiConfig.ConnectionConfirmationTimeout;
			OutputRegistrationTimeout = wabiSabiConfig.OutputRegistrationTimeout;
			TransactionSigningTimeout = wabiSabiConfig.TransactionSigningTimeout;
			BlameInputRegistrationTimeout = wabiSabiConfig.BlameInputRegistrationTimeout;

			BlameOf = blameOf;
			IsBlameRound = BlameOf is not null;
			BlameWhitelist = BlameOf
								 ?.Alices
								 .Select(x => x.Coin.Outpoint)
								 .Where(x => prison is null || !prison.IsBanned(x))
								 .ToHashSet()
							 ?? new HashSet<OutPoint>();
		}

		public WasabiRandom Random { get; }
		public FeeRate FeeRate { get; }
		public Network Network { get; }
		public int MinInputCountByRound { get; }
		public int MaxInputCountByRound { get; }
		public Money MinRegistrableAmount { get; }
		public Money MaxRegistrableAmount { get; }
		public Round? BlameOf { get; }
		public bool IsBlameRound { get; }
		public ISet<OutPoint> BlameWhitelist { get; }
		public TimeSpan StandardInputRegistrationTimeout { get; }
		public TimeSpan ConnectionConfirmationTimeout { get; }
		public TimeSpan OutputRegistrationTimeout { get; }
		public TimeSpan TransactionSigningTimeout { get; }
		public TimeSpan BlameInputRegistrationTimeout { get; }

		public static RoundParameters CreateBlameRoundParameters(
			WabiSabiConfig wabiSabiConfig,
			Network network,
			WasabiRandom random,
			FeeRate feeRate,
			Round blameOf,
			Prison prison)
		{
			return new RoundParameters(wabiSabiConfig, network, random, feeRate, blameOf, prison);
		}
	}
}
