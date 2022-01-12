using NBitcoin;
using Newtonsoft.Json;
using System.ComponentModel;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;

namespace WalletWasabi.CoinJoin.Coordinator.Rounds;

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

	[DefaultValue(604800)] // One week
	[JsonProperty(PropertyName = "InputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public long InputRegistrationTimeout { get; internal set; }

	[DefaultValue(60)]
	[JsonProperty(PropertyName = "ConnectionConfirmationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public long ConnectionConfirmationTimeout { get; internal set; }

	[DefaultValue(60)]
	[JsonProperty(PropertyName = "OutputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public long OutputRegistrationTimeout { get; internal set; }

	[DefaultValue(60)]
	[JsonProperty(PropertyName = "SigningTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public long SigningTimeout { get; internal set; }

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

	public Script GetNextCleanCoordinatorScript() => DeriveCoordinatorScript(CoordinatorExtPubKeyCurrentDepth);

	public Script DeriveCoordinatorScript(int index) => CoordinatorExtPubKey.Derive(0, false).Derive(index, false).PubKey.WitHash.ScriptPubKey;

	public void MakeNextCoordinatorScriptDirty()
	{
		CoordinatorExtPubKeyCurrentDepth++;
		ToFile();
	}

	public void UpdateOrDefault(CoordinatorRoundConfig config, bool toFile)
	{
		Denomination = config.Denomination ?? Denomination;
		var configSerialized = JsonConvert.SerializeObject(config);
		JsonConvert.PopulateObject(configSerialized, this);

		if (toFile)
		{
			ToFile();
		}
	}
}
