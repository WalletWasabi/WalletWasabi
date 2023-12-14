namespace WalletWasabi.Fluent.Models;

public record EnumValue<T>(T Value, string Name) where T : Enum
{
	public override string ToString()
	{
		return Name;
	}
}
