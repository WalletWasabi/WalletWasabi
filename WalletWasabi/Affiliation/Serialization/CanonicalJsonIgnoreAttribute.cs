namespace WalletWasabi.Affiliation.Serialization;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class CanonicalJsonIgnoreAttribute : Attribute
{
}
