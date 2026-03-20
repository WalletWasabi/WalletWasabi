using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models;

public class ServiceConfiguration
{
	public ServiceConfiguration(
		Money dustThreshold,
		int dropUnconfirmedTransactionsAfterDays = Constants.DefaultMaxDaysInMempool)
	{
		ArgumentNullException.ThrowIfNull(dustThreshold);

		DustThreshold = dustThreshold;
		DropUnconfirmedTransactionsAfterDays = dropUnconfirmedTransactionsAfterDays;
	}

	public Money DustThreshold { get; set; }
	public int DropUnconfirmedTransactionsAfterDays { get; }
}
