using System.Globalization;
using System.Reflection;

namespace WalletWasabi.JsonConverters;

public static class ReflectionUtils
{
	public static T? CreateInstance<T>(object[] args) =>
		(T?)Activator.CreateInstance(
			typeof(T),
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance,
			Type.DefaultBinder,
			args,
			CultureInfo.InvariantCulture);
}
