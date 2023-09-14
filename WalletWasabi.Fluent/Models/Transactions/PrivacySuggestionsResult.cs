using System.Collections.Generic;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Models.Transactions;

public record PrivacySuggestionsResult()
{
	private List<IAsyncEnumerable<PrivacyItem>> _combinedResults = new();

	public PrivacySuggestionsResult Combine(IAsyncEnumerable<PrivacyItem> results)
	{
		_combinedResults.Add(results);
		return this;
	}

	public PrivacySuggestionsResult Combine(IEnumerable<PrivacyItem> results)
	{
		_combinedResults.Add(results.ToAsyncEnumerable());
		return this;
	}

	public async IAsyncEnumerable<PrivacyItem> GetAllWarningsAndSuggestionsAsync()
	{
		foreach (var combined in _combinedResults)
		{
			await foreach (var item in combined)
			{
				yield return item;
			}
		}
	}
}
