using NBitcoin;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Coins;

namespace WalletWasabi.Transactions.TransactionBuilding
{
	public class SmartCoinSelector : ICoinSelector
	{
		private IEnumerable<SmartCoin> UnspentCoins { get; }

		public SmartCoinSelector(IEnumerable<SmartCoin> unspentCoins)
		{
			UnspentCoins = Guard.NotNull(nameof(unspentCoins), unspentCoins).Distinct();
		}

		public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
		{
			var targetMoney = target as Money;

			var available = UnspentCoins.Sum(x => x.Amount);
			if (available < targetMoney)
			{
				throw new InsufficientBalanceException(targetMoney, available);
			}

			// Group coins by scriptPubKey in order to treat them all as one.
			var coinsByScriptPubKey = UnspentCoins
				.GroupBy(c => c.ScriptPubKey)
				.Select(group => new
				{
					Coins = group,
					Unconfirmed = group.Any(x => !x.Confirmed),    // If group has an unconfirmed, then the whole group is unconfirmed.
					AnonymitySet = group.Min(x => x.AnonymitySet), // The group is as anonymous as its weakest member.
					Amount = group.Sum(x => x.Amount)
				});

			var coinsToSpend = new List<SmartCoin>();

			foreach (IGrouping<Script, SmartCoin> coinsGroup in coinsByScriptPubKey
				.OrderBy(group => group.Unconfirmed)
				.ThenByDescending(group => group.AnonymitySet)     // Always try to spend/merge the largest anonset coins first.
				.ThenByDescending(group => group.Amount)           // Then always try to spend by amount.
				.Select(group => group.Coins))
			{
				coinsToSpend.AddRange(coinsGroup);

				if (coinsToSpend.Sum(x => x.Amount) >= targetMoney)
				{
					break;
				}
			}

			return coinsToSpend.Select(c => c.GetCoin());
		}
	}
}
