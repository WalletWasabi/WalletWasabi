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

		/// <summary>
		/// How much weight units the server gives to alices per registrations.
		/// If it's 1000, then about 400 alices can participate.
		/// The width of the rangeproofs are calculated from this, so don't choose stupid numbers.
		/// Consider that it applies to registrations, not for inputs. This usually consists one input, but can be more.
		/// </summary>
		[DefaultValue(1000)]
		[JsonProperty(PropertyName = "RegistrableWeightCredentials", DefaultValueHandling = DefaultValueHandling.Populate)]
		public uint RegistrableWeightCredentials { get; set; } = 1000;

		[DefaultValue(true)]
		[JsonProperty(PropertyName = "AllowNotedInputRegistration", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool AllowNotedInputRegistration { get; set; } = true;

		[DefaultValueTimeSpan("0d 0h 1m 0s")]
		[JsonProperty(PropertyName = "ConnectionConfirmationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanJsonConverter))]
		public TimeSpan ConnectionConfirmationTimeout { get; set; } = TimeSpan.FromMinutes(1);

		[DefaultValueTimeSpan("0d 0h 1m 0s")]
		[JsonProperty(PropertyName = "OutputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanJsonConverter))]
		public TimeSpan OutputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(1);

		[DefaultValueTimeSpan("0d 0h 1m 0s")]
		[JsonProperty(PropertyName = "TransactionSigningTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanJsonConverter))]
		public TimeSpan TransactionSigningTimeout { get; set; } = TimeSpan.FromMinutes(1);
	}
}
