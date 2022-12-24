using System.Reflection;
using System.Runtime.CompilerServices;

namespace WalletWasabi.Extensions;

public static class MethodInfoExtensions
{
	public static bool IsAsync(this MethodInfo mi)
	{
		Type attType = typeof(AsyncStateMachineAttribute);

		var attrib = mi.GetCustomAttribute(attType) as AsyncStateMachineAttribute;

		return attrib is not null;
	}
}
