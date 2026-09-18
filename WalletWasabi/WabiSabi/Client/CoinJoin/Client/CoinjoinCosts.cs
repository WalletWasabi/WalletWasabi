namespace WalletWasabi.WabiSabi.Client;

/// <summary>
/// What a coinjoin transaction cost to the wallet: the share of the mining fee paid for the wallet's
/// inputs and outputs, the leftover amount that couldn't be decomposed into outputs (wasted dust) and
/// the total value of the payments made within the coinjoin.
/// </summary>
public record CoinjoinCosts(Money MiningFee, Money WastedDust, Money PaymentsTotal)
{
	public static readonly CoinjoinCosts Zero = new(Money.Zero, Money.Zero, Money.Zero);

	public Money TotalFee => MiningFee + WastedDust;

	public static CoinjoinCosts operator +(CoinjoinCosts left, CoinjoinCosts right) =>
		new(left.MiningFee + right.MiningFee, left.WastedDust + right.WastedDust, left.PaymentsTotal + right.PaymentsTotal);
}
