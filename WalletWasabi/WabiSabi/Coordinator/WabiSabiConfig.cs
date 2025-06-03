using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using NBitcoin;
using WalletWasabi.Bases;
using WalletWasabi.Discoverability;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;

namespace WalletWasabi.WabiSabi.Coordinator;

public class WabiSabiConfig : ConfigBase
{
	public WabiSabiConfig() : base("./fakeConfig.for.testing.only.json")
	{
	}

	public WabiSabiConfig(string filePath) : base(filePath)
	{
	}

	public Network Network { get; init; } = Network.Main;

	public string MainNetBitcoinRpcUri { get; } = Constants.DefaultMainNetBitcoinRpcUri;

	public string TestNetBitcoinRpcUri { get; } = Constants.DefaultTestNetBitcoinRpcUri;

	public string RegTestBitcoinRpcUri { get; } = Constants.DefaultRegTestBitcoinRpcUri;

	public string BitcoinRpcConnectionString { get; init; } = "user:password";

	public uint ConfirmationTarget { get; init; } = 108;

	public Money DoSSeverity { get; init; } = Money.Coins(0.1m);

	public TimeSpan DoSMinTimeForFailedToVerify { get; init; } = TimeSpan.FromDays(31);

	public TimeSpan DoSMinTimeForCheating { get; init; } = TimeSpan.FromDays(1);

	public double DoSPenaltyFactorForDisruptingConfirmation { get; init; } = 0.2;

	public double DoSPenaltyFactorForDisruptingSignalReadyToSign { get; init; } = 1.0;

	public double DoSPenaltyFactorForDisruptingSigning { get; init; } = 1.0;

	public double DoSPenaltyFactorForDisruptingByDoubleSpending { get; init; } = 3.0;

	public TimeSpan DoSMinTimeInPrison { get; init; } = TimeSpan.FromMinutes(20);

	public Money MinRegistrableAmount { get; init; } = Money.Coins(0.00005m);

	public Money MaxRegistrableAmount { get; init; } = Money.Coins(43_000m);

	public bool AllowNotedInputRegistration { get; set; } = true;

	public TimeSpan StandardInputRegistrationTimeout { get; init; } = TimeSpan.FromHours(1);

	public TimeSpan BlameInputRegistrationTimeout { get; init; } = TimeSpan.FromMinutes(3);

	public TimeSpan ConnectionConfirmationTimeout { get; set; } = TimeSpan.FromMinutes(1);

	public TimeSpan OutputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(1);

	public TimeSpan TransactionSigningTimeout { get; set; } = TimeSpan.FromMinutes(1);

	public TimeSpan FailFastOutputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(3);

	public TimeSpan FailFastTransactionSigningTimeout { get; set; } = TimeSpan.FromMinutes(1);

	public TimeSpan RoundExpiryTimeout { get; init; } = TimeSpan.FromMinutes(5);

	public int MaxInputCountByRound { get; set; } = 100;

	public double MinInputCountByRoundMultiplier { get; set; } = 0.5;

	public int MinInputCountByRound => Math.Max(1, (int)(MaxInputCountByRound * MinInputCountByRoundMultiplier));

	public double MinInputCountByBlameRoundMultiplier { get; set; } = 0.4;

	public int MinInputCountByBlameRound => Math.Max(1, (int)(MaxInputCountByRound * MinInputCountByBlameRoundMultiplier));

	public int RoundDestroyerThreshold { get; init; } = 375;

	public ExtPubKey CoordinatorExtPubKey { get; init; } = NBitcoinHelpers.BetterParseExtPubKey(Constants.WabiSabiFallBackCoordinatorExtPubKey);

	public int CoordinatorExtPubKeyCurrentDepth { get; set; } = 1;

	public Money MaxSuggestedAmountBase { get; init; } = Money.Coins(0.1m);

	public int RoundParallelization { get; init; } = 1;

	public bool WW200CompatibleLoadBalancing { get; init; } = false;

	public double WW200CompatibleLoadBalancingInputSplit { get; init; } = 0.75;

	public string CoordinatorIdentifier { get; set; } = "CoinJoinCoordinatorIdentifier";

	public bool AllowP2wpkhInputs { get; init; } = true;

	public bool AllowP2trInputs { get; init; } = true;

	public bool AllowP2wpkhOutputs { get; init; } = true;

	public bool AllowP2trOutputs { get; init; } = true;

	public bool AllowP2pkhOutputs { get; init; } = false;

	public bool AllowP2shOutputs { get; init; } = false;

	public bool AllowP2wshOutputs { get; init; } = false;

	public bool DelayTransactionSigning { get; init; } = false;

	public AnnouncerConfig AnnouncerConfig { get; set; } = new();

	public ImmutableSortedSet<ScriptType> AllowedInputTypes => GetScriptTypes(AllowP2wpkhInputs, AllowP2trInputs, false, false, false);

	public ImmutableSortedSet<ScriptType> AllowedOutputTypes => GetScriptTypes(AllowP2wpkhOutputs, AllowP2trOutputs, AllowP2pkhOutputs, AllowP2shOutputs, AllowP2wshOutputs);

	public Script GetNextCleanCoordinatorScript() => DeriveCoordinatorScript(CoordinatorExtPubKeyCurrentDepth);

	public Script DeriveCoordinatorScript(int index) => CoordinatorExtPubKey.Derive(0, false).Derive(index, false).PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);

	public void MakeNextCoordinatorScriptDirty()
	{
		CoordinatorExtPubKeyCurrentDepth++;
		if (!string.IsNullOrWhiteSpace(FilePath))
		{
			ToFile();
		}
	}

	public DoSConfiguration GetDoSConfiguration() =>
		new(
			SeverityInBitcoinsPerHour: DoSSeverity.ToDecimal(MoneyUnit.BTC),
			MinTimeForFailedToVerify: DoSMinTimeForFailedToVerify,
			MinTimeForCheating: DoSMinTimeForCheating,
			PenaltyFactorForDisruptingConfirmation: (decimal) DoSPenaltyFactorForDisruptingConfirmation,
			PenaltyFactorForDisruptingSignalReadyToSign: (decimal) DoSPenaltyFactorForDisruptingSignalReadyToSign,
			PenaltyFactorForDisruptingSigning: (decimal) DoSPenaltyFactorForDisruptingSigning,
			PenaltyFactorForDisruptingByDoubleSpending: (decimal) DoSPenaltyFactorForDisruptingByDoubleSpending,
			MinTimeInPrison: DoSMinTimeInPrison);

	private static ImmutableSortedSet<ScriptType> GetScriptTypes(bool p2wpkh, bool p2tr, bool p2pkh, bool p2sh, bool p2wsh)
	{
		var scriptTypes = new List<ScriptType>();
		if (p2wpkh)
		{
			scriptTypes.Add(ScriptType.P2WPKH);
		}
		if (p2tr)
		{
			scriptTypes.Add(ScriptType.Taproot);
		}
		if (p2pkh)
		{
			scriptTypes.Add(ScriptType.P2PKH);
		}
		if (p2sh)
		{
			scriptTypes.Add(ScriptType.P2SH);
		}
		if (p2wsh)
		{
			scriptTypes.Add(ScriptType.P2WSH);
		}

		// When adding new script types, please see
		// https://github.com/WalletWasabi/WalletWasabi/issues/5440

		return scriptTypes.ToImmutableSortedSet();
	}

	public static WabiSabiConfig LoadFile(string filePath)
	{
		try
		{
			using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
			var decoder = JsonDecoder.FromStream(Decode.WabiSabiConfig(filePath));
			var decodingResult = decoder(cfgFile);
			return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
		}
		catch (Exception ex)
		{
			var config = new WabiSabiConfig(filePath);
			config.ToFile();
			Logger.LogInfo($"{nameof(WabiSabiConfig)} file has been deleted because it was corrupted. Recreated default version at path: `{filePath}`.");
			Logger.LogWarning(ex);
			return config;
		}
	}

	protected override string EncodeAsJson() =>
		JsonEncoder.ToReadableString(this, Encode.WabiSabiConfig);
}
