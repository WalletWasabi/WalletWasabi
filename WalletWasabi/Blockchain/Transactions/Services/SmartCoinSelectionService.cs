using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Blockchain.Transactions.Services
{
	public class SmartCoinSelectionService
	{
		public SmartCoinSelectionService(bool allowUnconfirmed)
		{
			AllowUnconfirmed = allowUnconfirmed;
		}
		public bool AllowUnconfirmed { get; }

		public List<SmartCoin> GetAllowedSmartCoinInputs(ICoinsView availableCoinsView)
		{
			List<SmartCoin> smartCoinInputs = AllowUnconfirmed // Inputs that can be used to build the transaction.
					? availableCoinsView.ToList()
					: availableCoinsView.Confirmed().ToList();

			return smartCoinInputs;
		}

		public static List<SmartCoin> IntersectWithAllowedInputs(List<SmartCoin> smartCoinInputs, IEnumerable<OutPoint> allowedInputs)
		{
			smartCoinInputs = smartCoinInputs
								.Where(x => allowedInputs.Any(y => y.Hash == x.TransactionId && y.N == x.Index))
								.ToList();

			return smartCoinInputs;
		}

		public List<SmartCoin> AppendThoseWithTheSameScript(List<SmartCoin> smartCoinInputs, ICoinsView availableCoinsView)
		{
			var allScripts = smartCoinInputs.Select(x => x.ScriptPubKey).ToHashSet();
			foreach (var coin in availableCoinsView.Where(x => !smartCoinInputs.Any(y => x.TransactionId == y.TransactionId && x.Index == y.Index)))
			{
				if (!(AllowUnconfirmed || coin.Confirmed))
				{
					continue;
				}

				if (allScripts.Contains(coin.ScriptPubKey))
				{
					smartCoinInputs.Add(coin);
				}
			}

			return smartCoinInputs;
		}
	}
}
