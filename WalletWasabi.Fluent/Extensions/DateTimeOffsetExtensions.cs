namespace WalletWasabi.Fluent.Extensions;

public static class DateTimeOffsetExtensions
{
	public static string ToUserFacingString(this DateTimeOffset value, bool withTime = true) => value.DateTime.ToUserFacingString(withTime);
}
