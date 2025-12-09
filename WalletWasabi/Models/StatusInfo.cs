using WalletWasabi.FeeRateEstimation;

namespace WalletWasabi.Models;

public class StatusInfo
{
	public bool IsTorRunning { get; protected set; }
	public decimal UsdExchangeRate { get; protected set; }
	public FeeRateEstimations? FeeRates { get; protected set; }
	public int BestHeight { get; protected set; }
	public bool IsIndexerAvailable { get; protected set; }
}
