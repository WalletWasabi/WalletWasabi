using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.Models.Currency;

public abstract record CurrencyValue
{
	public static readonly Empty EmptyValue = new();

	public static CurrencyValue FromBtc(Amount? amount)
	{
		return amount switch
		{
			null => EmptyValue,
			Amount x when x == Amount.Invalid => new Invalid(),
			Amount x => new Valid(x.Btc.ToDecimal(NBitcoin.MoneyUnit.BTC))
		};
	}

	public static CurrencyValue FromUsd(Amount? amount)
	{
		return amount switch
		{
			null => EmptyValue,
			Amount x when x == Amount.Invalid => new Invalid(),
			Amount x => new Valid(x.UsdValue)
		};
	}

	public record Valid(decimal Value) : CurrencyValue;

	public record Invalid : CurrencyValue;

	public record Empty : CurrencyValue;

	public decimal? ToDecimal()
	{
		return this switch
		{
			Valid v => v.Value,
			_ => null
		};
	}

	public string ToInvariantFormatString()
	{
		var decimalValue = ToDecimal();
		return CurrencyInput.Format(decimalValue);
	}
}
