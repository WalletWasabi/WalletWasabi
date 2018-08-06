using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.JsonConverters;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Backend.Models.Responses
{
	public class CcjRunningRoundState
	{
		[JsonConverter(typeof(StringEnumConverter))]
		public CcjRoundPhase Phase { get; set; }

		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Denomination { get; set; }

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

		public static CcjRunningRoundState CloneExcept(CcjRunningRoundState state, long roundId, int registeredPeerCount)
		{
			return new CcjRunningRoundState
			{
				Phase = state.Phase,
				Denomination = state.Denomination,
				RegisteredPeerCount = registeredPeerCount,
				RequiredPeerCount = state.RequiredPeerCount,
				CoordinatorFeePercent = state.CoordinatorFeePercent,
				FeePerInputs = state.FeePerInputs,
				FeePerOutputs = state.FeePerOutputs,
				MaximumInputCountPerPeer = state.MaximumInputCountPerPeer,
				RegistrationTimeout = state.RegistrationTimeout,
				RoundId = roundId
			};
		}

		public Money CalculateRequiredAmount(params Money[] queuedCoinAmounts)
		{
			var tried = new List<Money>();
			Money baseMinimum = Denomination + Denomination.Percentange(CoordinatorFeePercent) * RequiredPeerCount + FeePerOutputs * 2;
			foreach (Money amount in queuedCoinAmounts.OrderByDescending(x => x))
			{
				tried.Add(amount);
				Money required = baseMinimum + FeePerInputs * tried.Count;
				if (required <= tried.Sum() || tried.Count == MaximumInputCountPerPeer)
				{
					return required;
				}
			}

			return baseMinimum + FeePerInputs * MaximumInputCountPerPeer;
		}

		public bool HaveEnoughQueued(params Money[] queuedCoinAmounts)
		{
			var tried = new List<Money>();
			Money baseMinimum = Denomination + Denomination.Percentange(CoordinatorFeePercent) * RequiredPeerCount + FeePerOutputs * 2;
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

			return false;
		}
	}
}
