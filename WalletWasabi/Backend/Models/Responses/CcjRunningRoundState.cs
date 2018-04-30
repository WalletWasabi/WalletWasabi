using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.ChaumianCoinJoin;
using WalletWasabi.JsonConverters;

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
	}
}
