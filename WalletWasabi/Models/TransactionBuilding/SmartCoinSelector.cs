using NBitcoin;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Models;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models.TransactionBuilding
{
	public class SmartCoinSelector : ICoinSelector
	{
		private Dictionary<OutPoint, SmartCoin> SmartCoinsByOutpoint { get; }

		public SmartCoinSelector(Dictionary<OutPoint, SmartCoin> smartCoinsByOutpoint)
		{
			SmartCoinsByOutpoint = Guard.NotNull(nameof(smartCoinsByOutpoint), smartCoinsByOutpoint);
		}

		public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
		{
			var coinsByOutpoint = coins.ToDictionary(c => c.Outpoint);
			var totalOutAmount = (Money)target;
			var unspentCoins = coins.Select(c => SmartCoinsByOutpoint[c.Outpoint]).ToArray();
			var coinsToSpend = new HashSet<SmartCoin>();
			var unspentConfirmedCoins = new List<SmartCoin>();
			var unspentUnconfirmedCoins = new List<SmartCoin>();
			foreach (SmartCoin coin in unspentCoins)
			{
				if (coin.Confirmed)
				{
					unspentConfirmedCoins.Add(coin);
				}
				else
				{
					unspentUnconfirmedCoins.Add(coin);
				}
			}

			bool haveEnough = TrySelectCoins(coinsToSpend, totalOutAmount, unspentConfirmedCoins);
			if (!haveEnough)
			{
				haveEnough = TrySelectCoins(coinsToSpend, totalOutAmount, unspentUnconfirmedCoins);
			}

			if (!haveEnough)
			{
				throw new InsufficientBalanceException(totalOutAmount, unspentConfirmedCoins.Select(x => x.Amount).Sum() + unspentUnconfirmedCoins.Select(x => x.Amount).Sum());
			}

			return coinsToSpend.Select(c => coinsByOutpoint[c.GetOutPoint()]);
		}

		/// <returns>If the selection was successful. If there's enough coins to spend from.</returns>
		private bool TrySelectCoins(HashSet<SmartCoin> coinsToSpend, Money totalOutAmount, IEnumerable<SmartCoin> unspentCoins)
		{
			// If there's no need for input merging, then use the largest selected.
			// Do not prefer anonymity set. You can assume the user prefers anonymity set manually through the GUI.
			SmartCoin largestCoin = unspentCoins.OrderByDescending(x => x.Amount).FirstOrDefault();
			if (largestCoin == default)
			{
				return false; // If there's no coin then unsuccessful selection.
			}
			else // Check if we can do without input merging.
			{
				var largestCoins = unspentCoins.Where(x => x.ScriptPubKey == largestCoin.ScriptPubKey);

				if (largestCoins.Sum(x => x.Amount) >= totalOutAmount)
				{
					foreach (var c in largestCoins)
					{
						coinsToSpend.Add(c);
					}
					return true;
				}
			}

			// If there's a need for input merging.
			foreach (var coin in unspentCoins
				.OrderByDescending(x => x.AnonymitySet) // Always try to spend/merge the largest anonset coins first.
				.ThenByDescending(x => x.Amount)) // Then always try to spend by amount.
			{
				coinsToSpend.Add(coin);
				// If reaches the amount, then return true, else just go with the largest coin.
				if (coinsToSpend.Select(x => x.Amount).Sum() >= totalOutAmount)
				{
					// Add if we can find address reuse.
					foreach (var c in unspentCoins
						.Except(coinsToSpend) // So we're choosing from the non selected coins.
						.Where(x => coinsToSpend.Any(y => y.ScriptPubKey == x.ScriptPubKey)))// Where the selected coins contains the same script.
					{
						coinsToSpend.Add(c);
					}

					return true;
				}
			}

			return false;
		}
	}
}
