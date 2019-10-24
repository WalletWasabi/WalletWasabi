using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Transactions.TransactionBuilding
{
	public class MoneyRequest
	{
		public static MoneyRequest Create(Money amount, bool subtractFee = false) => new MoneyRequest(amount, MoneyRequestType.Value, subtractFee);

		public static MoneyRequest CreateChange(bool subtractFee = true) => new MoneyRequest(null, MoneyRequestType.Change, subtractFee);

		public static MoneyRequest CreateAllRemaining(bool subtractFee = true) => new MoneyRequest(null, MoneyRequestType.AllRemaining, subtractFee);

		public Money Amount { get; }
		public MoneyRequestType Type { get; }
		public bool SubtractFee { get; }

		private MoneyRequest(Money amount, MoneyRequestType type, bool subtractFee)
		{
			if (type == MoneyRequestType.AllRemaining || type == MoneyRequestType.Change)
			{
				if (amount != null)
				{
					throw new ArgumentException($"{nameof(amount)} must be null.");
				}
			}
			else if (type == MoneyRequestType.Value)
			{
				if (amount is null)
				{
					throw new ArgumentNullException($"{nameof(amount)} cannot be null.");
				}
				else if (amount <= Money.Zero)
				{
					throw new ArgumentOutOfRangeException($"{nameof(amount)} must be positive.");
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
	}
}
