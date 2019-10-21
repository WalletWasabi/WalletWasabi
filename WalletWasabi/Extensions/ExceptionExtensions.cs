using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Exceptions;

namespace System
{
	public static class ExceptionExtensions
	{
		public static string ToTypeMessageString(this Exception ex)
		{
			var trimmed = Guard.Correct(ex.Message);

			if (trimmed == "")
			{
				if (ex is HwiException hwiEx)
				{
					return $"{hwiEx.GetType().Name}: {hwiEx.ErrorCode}";
				}
				return ex.GetType().Name;
			}
			else
			{
				return $"{ex.GetType().Name}: {ex.Message}";
			}
		}

		public static Dictionary<string, string> BitcoinCoreTranslations { get; } = new Dictionary<string, string>
		{
			["too-long-mempool-chain"] = "At least one coin you are trying to spend is part of long or heavy chain of unconfirmed transactions. You must wait for some previous transactions to confirm.",
			["bad-txns-inputs-missingorspent"] = "At least one coin you are trying to spend is already spent.",
			["missing-inputs"] = "At least one coin you are trying to spend is already spent.",
			["txn-mempool-conflict"] = "At least one coin you are trying to spend is already spent.",
			["bad-txns-inputs-duplicate"] = "The transaction contains duplicated inputs.",
			["bad-txns-nonfinal"] = "The transaction is not final and cannot be broadcasted.",
			["bad-txns-oversize"] = "The transaction is too big."
		};

		public static string ToUserFriendlyString(this HttpRequestException ex)
		{
			var trimmed = Guard.Correct(ex.Message);
			if (trimmed == "")
			{
				return ex.ToTypeMessageString();
			}
			else
			{
				foreach (KeyValuePair<string, string> pair in BitcoinCoreTranslations)
				{
					if (trimmed.Contains(pair.Key, StringComparison.InvariantCultureIgnoreCase))
					{
						return pair.Value;
					}
				}

				return ex.ToTypeMessageString();
			}
		}
	}
}
