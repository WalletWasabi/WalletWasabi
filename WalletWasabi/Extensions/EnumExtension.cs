using System;
using System.ComponentModel;
using System.Linq;
using WalletWasabi.Models;

namespace WalletWasabi.Extensions
{
	public static class EnumExtensions
	{
		public static T? GetFirstAttribute<T>(this Enum value) where T : Attribute
		{
			var type = value.GetType();
			var memberInfo = type.GetMember(value.ToString());
			var attributes = memberInfo[0].GetCustomAttributes(typeof(T), false);

			return attributes.Any() ? (T)attributes[0] : null;
		}

		public static string FriendlyName(this Enum value)
		{
			var attribute = value.GetFirstAttribute<FriendlyNameAttribute>();

			return attribute is { } ? attribute.FriendlyName : value.ToString();
		}
	}
}
