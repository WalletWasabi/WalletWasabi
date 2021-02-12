using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.JsonConverters.Collections;
using WalletWasabi.JsonConverters.Timing;

namespace WalletWasabi.WabiSabi.Backend
{
	[JsonObject(MemberSerialization.OptIn)]
	public class WabiSabiConfig : ConfigBase
	{
		public WabiSabiConfig() : base()
		{
		}

		public WabiSabiConfig(string filePath) : base(filePath)
		{
		}

		[DefaultValue(Constants.OneDayConfirmationTarget)]
		[JsonProperty(PropertyName = "ConfirmationTarget", DefaultValueHandling = DefaultValueHandling.Populate)]
		public uint ConfirmationTarget { get; set; } = Constants.OneDayConfirmationTarget;

		[DefaultValueTimeSpan("0d 3h 0m 0s")]
		[JsonProperty(PropertyName = "ReleaseUtxoFromPrisonAfter", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanJsonConverter))]
		public TimeSpan ReleaseUtxoFromPrisonAfter { get; set; } = TimeSpan.FromHours(3);

		[DefaultValueStringCollection("[\"witness_v0_keyhash\"]")]
		[JsonProperty(PropertyName = "AllowedScriptTypes", DefaultValueHandling = DefaultValueHandling.Populate)]
		public IEnumerable<string> AllowedScriptTypes { get; set; } = new[] { "witness_v0_keyhash" };

		[DefaultValue(2)]
		[JsonProperty(PropertyName = "MaxInputCountByAlice", DefaultValueHandling = DefaultValueHandling.Populate)]
		public uint MaxInputCountByAlice { get; set; } = 2;

		[DefaultValueMoneyBtc("0.00005")]
		[JsonProperty(PropertyName = "MinRegistrableAmount", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money MinRegistrableAmount { get; set; } = Money.Coins(0.00005m);

		/// <summary>
		/// The width of the rangeproofs are calculated from this, so don't choose stupid numbers.
		/// </summary>
		[DefaultValueMoneyBtc("43000")]
		[JsonProperty(PropertyName = "MaxRegistrableAmount", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money MaxRegistrableAmount { get; set; } = Money.Coins(43000m);

		[DefaultValue(1)]
		[JsonProperty(PropertyName = "MinRegistrableWeight", DefaultValueHandling = DefaultValueHandling.Populate)]
		public uint MinRegistrableWeight { get; set; } = 1;

		/// <summary>
		/// The width of the rangeproofs are calculated from this, so don't choose stupid numbers.
		/// Consider that it applies to registrations, not for inputs. This usually consists one input, but can be more.
		/// 1000 / inputs looks good, so for 2 inputs it'd be 2000.
		/// </summary>
		[DefaultValue(2000)]
		[JsonProperty(PropertyName = "MaxRegistrableWeight", DefaultValueHandling = DefaultValueHandling.Populate)]
		public uint MaxRegistrableWeight { get; set; } = 2000;
	}
}
