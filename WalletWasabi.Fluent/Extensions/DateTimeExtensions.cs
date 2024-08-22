namespace WalletWasabi.Fluent.Extensions;

public static class DateTimeExtensions
{
	public static string ToUserFacingString(this DateTime value, bool withTime = true)
	{
		return value.ToString(withTime ? "HH:mm on MMMM d, yyyy" : "MMMM d, yyyy");
	}

	public static string ToUserFacingFriendlyString(this DateTime value)
	{
		var time = value.ToString("HH:mm");

		if (value.Date == DateTime.Today)
		{
			return $"Today, {time}";
		}

		if (value.Date == DateTime.Today.AddDays(-1))
		{
			return $"Yesterday, {time}";
		}

		return value.ToString("MMM d, yyyy");
	}
}
