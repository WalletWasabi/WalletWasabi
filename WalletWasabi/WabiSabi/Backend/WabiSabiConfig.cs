using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
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
		public int ConfirmationTarget { get; set; } = Constants.OneDayConfirmationTarget;

		[DefaultValueTimeSpan("0d 3h 0m 0s")]
		[JsonProperty(PropertyName = "ReleaseUtxoFromPrisonAfter", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanJsonConverter))]
		public TimeSpan ReleaseUtxoFromPrisonAfter { get; set; } = TimeSpan.FromHours(3);

		[DefaultValueStringCollection("[\"witness_v0_keyhash\"]")]
		[JsonProperty(PropertyName = "AllowedScriptTypes", DefaultValueHandling = DefaultValueHandling.Populate)]
		public IEnumerable<string> AllowedScriptTypes { get; set; } = new[] { "witness_v0_keyhash" };
	}
}
