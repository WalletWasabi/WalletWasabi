namespace WalletWasabi.Fluent.Extensions;

public static class DateTimeOffsetExtensions
{
	public static string ToUserFacingString(this DateTimeOffset value, bool withTime = true) => value.LocalDateTime.ToUserFacingString(withTime);

	public static string ToUserFacingFriendlyString(this DateTimeOffset value) => value.LocalDateTime.ToUserFacingFriendlyString();

	public static string ToOnlyTimeString(this DateTimeOffset value) => value.ToString("HH:mm", Lang.Resources.Culture);
}
