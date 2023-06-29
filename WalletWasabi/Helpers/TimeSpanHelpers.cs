namespace WalletWasabi.Helpers;

class TimeSpanHelpers
{
	public static TimeSpan Min(TimeSpan first, TimeSpan second) => first < second ? first : second;
}
