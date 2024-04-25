using WalletWasabi.Blockchain.Analysis.FeesEstimation;

namespace WalletWasabi.Services.Events;

public enum FeeRateSource
{
	Backend,
	LocalNodeRpc,
}

public record ExchangeRateChanged(decimal UsdBtcRate);
public record MiningFeeRatesChanged(FeeRateSource Source, AllFeeEstimate AllFeeEstimate);
public record ServerTipHeightChanged(uint Height);
public record ConnectionStateChanged(bool Connected);
public record SoftwareVersionChanged(Version ClientVersion, Version ServerVersion);
public record LegalDocumentVersionChanged(Version Version);
