namespace WalletWasabi.Fluent.Extensions;

public static class FunctionalExtensions
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
}
