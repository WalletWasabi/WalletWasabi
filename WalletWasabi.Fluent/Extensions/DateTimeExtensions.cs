namespace WalletWasabi.Fluent.Extensions;

public static class DateTimeExtensions
{
	public static string ToUserFacingString(this DateTime value) => value.ToString("yyyy-MM-dd HH:mm");
	public static string ToUserFacingDateString(this DateTime value) => value.ToString("yyyy-MM-dd");
}
