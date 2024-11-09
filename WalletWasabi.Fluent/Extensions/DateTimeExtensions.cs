namespace WalletWasabi.Fluent.Extensions;

public static class DateTimeExtensions
{
	public static string ToUserFacingString(this DateTime value, bool withTime = true)
	{
		return value.ToString(withTime ? $"HH:mm {Lang.Utils.LowerCaseFirst(Lang.Resources.Words_On)} MMMM d, yyyy" : "MMMM d, yyyy", Lang.Resources.Culture);
	}

	public static string ToUserFacingFriendlyString(this DateTime value)
	{
		var time = value.ToString("HH:mm", Lang.Resources.Culture);

		if (value.Date == DateTime.Today)
		{
			return $"{Lang.Resources.Sentences_Today_at} {time}";
		}

		if (value.Date == DateTime.Today.AddDays(-1))
		{
			return $"{Lang.Resources.Sentences_Yesterday_at} {time}";
		}

		return value.ToString("MMM d, yyyy", Lang.Resources.Culture);
	}
}
