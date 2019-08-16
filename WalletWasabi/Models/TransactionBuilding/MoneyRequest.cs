using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Models.TransactionBuilding
{
	public class MoneyRequest
	{
		public static MoneyRequest Create(Money amount) => new MoneyRequest(amount, MoneyRequestType.Value);

		public static MoneyRequest CreateAllRemaining() => new MoneyRequest(null, MoneyRequestType.AllRemaining);

		public Money Amount { get; }
		public MoneyRequestType Type { get; }

		private MoneyRequest(Money amount, MoneyRequestType type)
		{
			if (type == MoneyRequestType.AllRemaining)
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
		}
	}
}
