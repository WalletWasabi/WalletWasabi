using WalletWasabi.Models;

namespace WalletWasabi.Fluent.Models;

public enum TimePeriodOption
{
	All,
	[FriendlyName("1D")]
	Day,
	[FriendlyName("1W")]
	Week,
	[FriendlyName("1M")]
	Month,
	[FriendlyName("3M")]
	ThreeMonths,
	[FriendlyName("6M")]
	SixMonths,
	[FriendlyName("1Y")]
	Year
}
