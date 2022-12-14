namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

internal static class Mixin 
{
	public static T? Ensure<T>(this T? obj, Func<T, bool> condition) 
	{
		if (obj == null)
		{
			return default;
		}
		
		if (condition(obj)) 
		{
			return obj;	
		}
		
		return default;
	}

	public static TResult? Try<T, TResult>(this T? obj, Func<T, TResult> map)
	{
		if (obj == null)
		{
			return default;
		}

		return map(obj);
	}
}
