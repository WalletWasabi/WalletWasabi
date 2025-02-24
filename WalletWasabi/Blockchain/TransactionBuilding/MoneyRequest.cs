using NBitcoin;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public abstract record MoneyRequest(bool SubtractFee)
{
	public record AllRemaining(bool SubtractFee) : MoneyRequest(SubtractFee);
	public record Change(bool SubtractFee) : MoneyRequest(SubtractFee);
	public record Value : MoneyRequest
	{
		public Value(Money amount, bool subtractFee) : base(subtractFee)
		{
			Amount = amount > Money.Zero
				? amount
				: throw new ArgumentOutOfRangeException(nameof(amount), amount.ToString(false), "Must be positive.");
		}

		public Money Amount { get; }
	}

	public static MoneyRequest Create(Money amount, bool subtractFee = false) => new Value(amount, subtractFee);

	public static MoneyRequest CreateChange(bool subtractFee = true) => new Change(subtractFee);

	public static MoneyRequest CreateAllRemaining(bool subtractFee = true) => new AllRemaining(subtractFee);
}
