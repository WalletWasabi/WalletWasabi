using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin
{
	public class CcjRunningRoundState
	{
		[JsonConverter(typeof(StringEnumConverter))]
		public CcjRoundPhase Phase { get; set; }

		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Denomination { get; set; }

		[JsonConverter(typeof(BlockCypherDateTimeOffsetJsonConverter))]
		public DateTimeOffset InputRegistrationTimesout { get; set; }

		public IEnumerable<SchnorrPubKey> SchnorrPubKeys { get; set; }

		public int RegisteredPeerCount { get; set; }

		public int RequiredPeerCount { get; set; }

		public int MaximumInputCountPerPeer { get; set; }

		public int RegistrationTimeout { get; set; }

		[JsonConverter(typeof(MoneySatoshiJsonConverter))]
		public Money FeePerInputs { get; set; }

		[JsonConverter(typeof(MoneySatoshiJsonConverter))]
		public Money FeePerOutputs { get; set; }

		public decimal CoordinatorFeePercent { get; set; }

		public long RoundId { get; set; }

		/// <summary>
		/// This is round independent, it is only here because of backward compatibility.
		/// </summary>
		public int SuccessfulRoundCount { get; set; }

		public Money CalculateRequiredAmount(params Money[] queuedCoinAmounts)
		{
			var tried = new List<Money>();
			Money baseMinimum = Denomination + FeePerOutputs * 2; // + (Denomination.Percentange(CoordinatorFeePercent) * RequiredPeerCount);
			if (queuedCoinAmounts != default)
			{
				foreach (Money amount in queuedCoinAmounts.OrderByDescending(x => x))
				{
					tried.Add(amount);
					Money required = baseMinimum + FeePerInputs * tried.Count;
					if (required <= tried.Sum() || tried.Count == MaximumInputCountPerPeer)
					{
						return required;
					}
				}
			}

			return baseMinimum + FeePerInputs;
			//return baseMinimum + (FeePerInputs * MaximumInputCountPerPeer);
		}

		public bool HaveEnoughQueued(params Money[] queuedCoinAmounts)
		{
			var tried = new List<Money>();
			Money baseMinimum = Denomination + FeePerOutputs * 2; // + (Denomination.Percentange(CoordinatorFeePercent) * RequiredPeerCount);

			if (queuedCoinAmounts != default)
			{
				foreach (Money amount in queuedCoinAmounts.OrderByDescending(x => x))
				{
					tried.Add(amount);
					Money required = baseMinimum + FeePerInputs * tried.Count;
					if (required <= tried.Sum())
					{
						return true;
					}
					if (tried.Count == MaximumInputCountPerPeer)
					{
						return false;
					}
				}
			}

			return false;
		}
	}
}
