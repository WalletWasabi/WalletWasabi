using NBitcoin;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Coordinator.Rounds
{
	[JsonObject(MemberSerialization.OptIn)]
	public class CoordinatorRoundConfig : ConfigBase
	{
		public CoordinatorRoundConfig() : base()
		{
		}

		public CoordinatorRoundConfig(string filePath) : base(filePath)
		{
		}

		[JsonProperty(PropertyName = "Denomination")]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Denomination { get; internal set; } = Money.Coins(0.1m);

		[DefaultValue(Constants.OneDayConfirmationTarget)]
		[JsonProperty(PropertyName = "ConfirmationTarget", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int ConfirmationTarget { get; internal set; }

		[DefaultValue(0.7)]
		[JsonProperty(PropertyName = "ConfirmationTargetReductionRate", DefaultValueHandling = DefaultValueHandling.Populate)]
		public double ConfirmationTargetReductionRate { get; internal set; }

		[DefaultValue(0.003)] // Coordinator fee percent is per anonymity set.
		[JsonProperty(PropertyName = "CoordinatorFeePercent", DefaultValueHandling = DefaultValueHandling.Populate)]
		public decimal CoordinatorFeePercent { get; internal set; }

		[DefaultValue(100)]
		[JsonProperty(PropertyName = "AnonymitySet", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int AnonymitySet { get; internal set; }

		[JsonProperty(PropertyName = "InputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanSecondsConverter))]
		public TimeSpan InputRegistrationTimeout { get; internal set; } = TimeSpan.FromDays(7);

		[JsonProperty(PropertyName = "ConnectionConfirmationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanSecondsConverter))]
		public TimeSpan ConnectionConfirmationTimeout { get; internal set; } = TimeSpan.FromMinutes(1);

		[JsonProperty(PropertyName = "OutputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanSecondsConverter))]
		public TimeSpan OutputRegistrationTimeout { get; internal set; } = TimeSpan.FromMinutes(1);

		[JsonProperty(PropertyName = "SigningTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
		[JsonConverter(typeof(TimeSpanSecondsConverter))]
		public TimeSpan SigningTimeout { get; internal set; } = TimeSpan.FromMinutes(1);

		[DefaultValue(1)]
		[JsonProperty(PropertyName = "DosSeverity", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int DosSeverity { get; internal set; }

		[DefaultValue(730)] // 1 month
		[JsonProperty(PropertyName = "DosDurationHours", DefaultValueHandling = DefaultValueHandling.Populate)]
		public long DosDurationHours { get; internal set; }

		[DefaultValue(true)]
		[JsonProperty(PropertyName = "DosNoteBeforeBan", DefaultValueHandling = DefaultValueHandling.Populate)]
		public bool DosNoteBeforeBan { get; internal set; }

		[DefaultValue(11)]
		[JsonProperty(PropertyName = "MaximumMixingLevelCount", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int MaximumMixingLevelCount { get; internal set; }

		[JsonProperty(PropertyName = "CoordinatorExtPubKey")]
		[JsonConverter(typeof(ExtPubKeyJsonConverter))]
		public ExtPubKey CoordinatorExtPubKey { get; private set; } = Constants.FallBackCoordinatorExtPubKey;

		[DefaultValue(0)]
		[JsonProperty(PropertyName = "CoordinatorExtPubKeyCurrentDepth", DefaultValueHandling = DefaultValueHandling.Populate)]
		public int CoordinatorExtPubKeyCurrentDepth { get; private set; }

		public Script GetNextCleanCoordinatorScript() => CoordinatorExtPubKey.Derive(0, false).Derive(CoordinatorExtPubKeyCurrentDepth, false).PubKey.WitHash.ScriptPubKey;

		public async Task MakeNextCoordinatorScriptDirtyAsync()
		{
			CoordinatorExtPubKeyCurrentDepth++;
			await ToFileAsync().ConfigureAwait(false);
		}

		public async Task UpdateOrDefaultAsync(CoordinatorRoundConfig config, bool toFile)
		{
			Denomination = config.Denomination ?? Denomination;
			var configSerialized = JsonConvert.SerializeObject(config);
			JsonConvert.PopulateObject(configSerialized, this);

			if (toFile)
			{
				await ToFileAsync().ConfigureAwait(false);
			}
		}
	}
}
