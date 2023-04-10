namespace WalletWasabi.Fluent.Extensions;

public static class DateTimeOffsetExtensions
{
	public static string ToUserFacingString(this DateTimeOffset value) => value.DateTime.ToUserFacingString();
	public static string ToUserFacingDateString(this DateTimeOffset value) => value.DateTime.ToUserFacingDateString();
}
