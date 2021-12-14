using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PocketSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private List<string> _labels;
	[AutoNotify] private List<string> _filteredLabels;

	public PocketSuggestionViewModel(SmartLabel currentLabels) : base(null)
	{
		Labels = currentLabels.Take(1).ToList();
		FilteredLabels = currentLabels.Skip(1).ToList();
	}
}