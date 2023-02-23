namespace WalletWasabi.Affiliation.Models.CoinJoinNotification;

public record CoordinatorFeeRate(decimal FeeRate)
{
	public static implicit operator CoordinatorFeeRate(decimal feeRate) => new(feeRate);
}
