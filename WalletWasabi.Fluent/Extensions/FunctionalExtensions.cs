namespace WalletWasabi.Fluent.Extensions;

public static class FunctionalExtensions
{
	public static T? Ensure<T>(this T? obj, Func<T, bool> condition) where T : struct
	{
		if (obj is null)
		{
			return default;
		}

		if (condition(obj.Value))
		{
			return obj;
		}

		return default;
	}

	public static T? Ensure<T>(this T? obj, Func<T, bool> condition) where T : class
	{
		if (obj is null)
		{
			return default;
		}

		if (condition(obj))
		{
			return obj;
		}

		return default;
	}
}
