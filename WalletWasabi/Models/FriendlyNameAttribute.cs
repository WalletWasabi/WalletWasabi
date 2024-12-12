namespace WalletWasabi.Models;

[AttributeUsage(AttributeTargets.Field)]
public class FriendlyNameAttribute : Attribute
{
	public FriendlyNameAttribute(string? friendlyName = null, bool isLocalized = false)
	{
		FriendlyName = friendlyName;
		IsLocalized = isLocalized;
	}

	public string? FriendlyName { get; }

	public bool IsLocalized { get; }
}
