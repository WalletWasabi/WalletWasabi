namespace WalletWasabi.Models;

[AttributeUsage(AttributeTargets.Field)]
public class FriendlyNameAttribute : Attribute
{
	public FriendlyNameAttribute(string friendlyName)
	{
		FriendlyName = friendlyName;
	}

	public string FriendlyName { get; }
}
