namespace WalletWasabi.Fluent.Extensions;

public static class DateTimeExtensions
{
	public static string ToUserFacingString(this DateTime value, bool withTime = true) => value.ToString(withTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd");
}
