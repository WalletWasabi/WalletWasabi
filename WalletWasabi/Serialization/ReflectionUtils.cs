using System.Globalization;
using System.Reflection;

namespace WalletWasabi.JsonConverters;

public static class ReflectionUtils
{
	public static T CreateInstance<T>(object[] args) =>
		(T?)Activator.CreateInstance(
			typeof(T),
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance,
			Type.DefaultBinder,
			args,
			CultureInfo.InvariantCulture) ?? throw new InvalidOperationException($"It was not possible to create an instance of '{typeof(T).FullName}'");

	public static Func<TType, TValue> GetPropertyAccessor<TType, TValue>(string propertyName)
	{
		var property = typeof(TType).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
		return instance => (TValue)property.GetValue(instance);
	}
}
