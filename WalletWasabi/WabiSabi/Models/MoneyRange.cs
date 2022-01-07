using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public record MoneyRange(Money Min, Money Max)
{
	public bool Contains(Money value) =>
		value >= Min && value <= Max;
}
