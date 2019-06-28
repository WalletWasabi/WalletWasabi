using System.Collections.Generic;
using System.Net.Http;
using WalletWasabi.Helpers;

namespace System
{
	public static class ExceptionExtensions
	{
		public static string ToTypeMessageString(this Exception ex)
		{
			var trimmed = Guard.Correct(ex.Message);
			if (trimmed == "")
			{
				return ex.GetType().Name;
			}
			else
			{
				return $"{ex.GetType().Name}: {ex.Message}";
			}
		}

		public static Dictionary<string, string> BitcoinCoreTransalations { get; } = new Dictionary<string, string> {
			["too-long-mempool-chain"] = "At least one coin you are trying to spend is part of long chain of unconfirmed transactions. You must wait for some previous transactions to confirm.",
			["bad-txns-inputs-missingorspent"] = "At least one coin you are trying to spend is already spent.",
			["missing-inputs"] = "At least one coin you are trying to spend is already spent.",
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
				foreach (KeyValuePair<string, string> pair in BitcoinCoreTransalations)
				{
					if (trimmed.Contains(pair.Key))
					{
						return pair.Value;
					}
				}

				return ex.ToTypeMessageString();
			}
		}
	}
}
