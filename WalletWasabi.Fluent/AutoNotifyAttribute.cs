namespace WalletWasabi.Fluent;

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
sealed class AutoNotifyAttribute : Attribute
{
	public AutoNotifyAttribute()
	{
	}

	public string? PropertyName { get; set; }

	public AccessModifier SetterModifier { get; set; } = AccessModifier.Public;
}
