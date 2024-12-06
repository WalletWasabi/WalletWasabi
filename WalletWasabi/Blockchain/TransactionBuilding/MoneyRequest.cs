using NBitcoin;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class MoneyRequest
{
	private MoneyRequest(Money? amount, MoneyRequestType type, bool subtractFee)
	{
		if (type is MoneyRequestType.AllRemaining or MoneyRequestType.Change)
		{
			if (amount is not null)
			{
				throw new ArgumentException("Must be null.", nameof(amount));
			}
		}
		else if (type == MoneyRequestType.Value)
		{
			if (amount is null)
			{
				throw new ArgumentNullException(nameof(amount));
			}
			else if (amount <= Money.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(amount), amount.ToString(false, true), "Must be positive.");
			}
		}
		else
		{
			throw new NotSupportedException($"{nameof(type)} is not supported: {type}.");
		}

		Amount = amount;
		Type = type;
		SubtractFee = subtractFee;
	}

	public Money? Amount { get; }
	public MoneyRequestType Type { get; }
	public bool SubtractFee { get; }

	public static MoneyRequest Create(Money amount, bool subtractFee = false) => new(amount, MoneyRequestType.Value, subtractFee);

	public static MoneyRequest CreateChange(bool subtractFee = true) => new(null, MoneyRequestType.Change, subtractFee);

	public static MoneyRequest CreateAllRemaining(bool subtractFee = true) => new(null, MoneyRequestType.AllRemaining, subtractFee);
}
