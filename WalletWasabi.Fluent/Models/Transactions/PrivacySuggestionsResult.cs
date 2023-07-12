using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Fluent.Models.Transactions;

public record PrivacySuggestionsResult
{
	public List<PrivacyWarning> Warnings { get; } = new();
	public List<PrivacySuggestion> Suggestions { get; } = new();

	public PrivacySuggestionsResult Combine(PrivacySuggestionsResult other)
	{
		Warnings.AddRange(other.Warnings);
		Suggestions.AddRange(other.Suggestions);

		return this;
	}
}
