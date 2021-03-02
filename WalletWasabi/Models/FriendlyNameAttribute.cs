using System;

namespace WalletWasabi.Models
{
	[AttributeUsage(AttributeTargets.Field)]
	public class FriendlyNameAttribute : Attribute
	{
		public string FriendlyName { get; }

		public FriendlyNameAttribute(string friendlyName)
		{
			FriendlyName = friendlyName;
		}
	}
}
