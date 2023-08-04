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

	public async IAsyncEnumerable<PrivacyWarning> GetAllWarningsAsync()
	{
		foreach (var combined in _combinedResults)
		{
			await foreach (var item in combined)
			{
				if (item is PrivacyWarning warning)
				{
					yield return warning;
				}
			}
		}
	}

	public async IAsyncEnumerable<PrivacySuggestion> GetAllSuggestionsAsync()
	{
		foreach (var combined in _combinedResults)
		{
			await foreach (var item in combined)
			{
				if (item is PrivacySuggestion suggestion)
				{
					yield return suggestion;
				}
			}
		}
	}
}
