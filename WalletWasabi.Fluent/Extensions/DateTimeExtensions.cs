namespace WalletWasabi.Fluent.Extensions;

public static class DateTimeExtensions
{
	public static string ToUserFacingString(this DateTime value, bool withTime = true)
	{
		return value.ToString(withTime ? "MMMM dd, yyyy HH:mm" : "MMMM dd, yyyy");
	}

	public static string ToUserFacingFriendlyString(this DateTime value)
	{
		if (value.Date == DateTime.Today)
		{
			return "Today";
		}

		return value.ToString("MMMM dd, yyyy");
	}
}
