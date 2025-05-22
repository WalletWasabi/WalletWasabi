using System.Net;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models;

public class ServiceConfiguration
{
	public ServiceConfiguration(
		string bitcoinRpcUri,
		Money dustThreshold,
		int dropUnconfirmedTransactionsAfterDays = Constants.DefaultMaxDaysInMempool)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(bitcoinRpcUri);
		ArgumentNullException.ThrowIfNull(dustThreshold);

		BitcoinRpcUri = new Uri(bitcoinRpcUri);
		DustThreshold = dustThreshold;
		DropUnconfirmedTransactionsAfterDays = dropUnconfirmedTransactionsAfterDays;
	}

	public Uri BitcoinRpcUri { get; set; }
	public Money DustThreshold { get; set; }
	public int DropUnconfirmedTransactionsAfterDays { get; }
}
