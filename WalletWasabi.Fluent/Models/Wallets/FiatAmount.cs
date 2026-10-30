namespace WalletWasabi.Fluent.Models.Wallets;

public record FiatAmount(decimal Value, string Currency)
{
	public FiatAmount Abs() => this with { Value = Math.Abs(Value) };
}
