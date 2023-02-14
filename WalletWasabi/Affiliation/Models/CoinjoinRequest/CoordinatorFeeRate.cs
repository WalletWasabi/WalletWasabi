namespace WalletWasabi.Affiliation.Models.CoinjoinRequest;

public record CoordinatorFeeRate(decimal FeeRate)
{
	public static implicit operator CoordinatorFeeRate(decimal feeRate) => new(feeRate);
}
