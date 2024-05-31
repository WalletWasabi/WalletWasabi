using System.Net;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models;

public class ServiceConfiguration
{
	public ServiceConfiguration(
		EndPoint bitcoinCoreEndPoint,
		Money dustThreshold,
		int dropUnconfirmedTransactionsAfterDays = 30)
	{
		BitcoinCoreEndPoint = Guard.NotNull(nameof(bitcoinCoreEndPoint), bitcoinCoreEndPoint);
		DustThreshold = Guard.NotNull(nameof(dustThreshold), dustThreshold);
		DropUnconfirmedTransactionsAfterDays = dropUnconfirmedTransactionsAfterDays;
	}

	public EndPoint BitcoinCoreEndPoint { get; set; }
	public Money DustThreshold { get; set; }
	public int DropUnconfirmedTransactionsAfterDays { get; }
}
