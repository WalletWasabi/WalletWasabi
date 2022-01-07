using System.Net;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models;

public class ServiceConfiguration
{
	public ServiceConfiguration(
		int minAnonScoreTarget,
		int maxAnonScoreTarget,
		EndPoint bitcoinCoreEndPoint,
		Money dustThreshold)
	{
		MinAnonScoreTarget = Guard.NotNull(nameof(minAnonScoreTarget), minAnonScoreTarget);
		MaxAnonScoreTarget = Guard.NotNull(nameof(maxAnonScoreTarget), maxAnonScoreTarget);
		BitcoinCoreEndPoint = Guard.NotNull(nameof(bitcoinCoreEndPoint), bitcoinCoreEndPoint);
		DustThreshold = Guard.NotNull(nameof(dustThreshold), dustThreshold);
	}

	public int MinAnonScoreTarget { get; set; }
	public int MaxAnonScoreTarget { get; set; }
	public EndPoint BitcoinCoreEndPoint { get; set; }
	public Money DustThreshold { get; set; }
}
