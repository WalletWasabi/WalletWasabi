using System.Collections.Generic;
using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.Transactions;

public record PrivacySuggestionsResult(IAsyncEnumerable<PrivacyItem> Items)
{
	private List<IAsyncEnumerable<PrivacyItem>> _combinedResults = new();

	public static PrivacySuggestionsResult CreateNew()
	{
		static async IAsyncEnumerable<PrivacyItem> EmptyItemsAsync()
		{
			yield break;
		};

		return new PrivacySuggestionsResult(EmptyItemsAsync());
	}

	public PrivacySuggestionsResult Combine(IAsyncEnumerable<PrivacyItem> results)
	{
		_combinedResults.Add(results);
		return this;
	}

	public async IAsyncEnumerable<PrivacyWarning> GetAllWarningsAsync()
	{
		await foreach (var item in Items)
		{
			if (item is PrivacyWarning warning)
			{
				yield return warning;
			}
		}

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
		await foreach (var item in Items)
		{
			if (item is PrivacySuggestion suggestion)
			{
				yield return suggestion;
			}
		}

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
