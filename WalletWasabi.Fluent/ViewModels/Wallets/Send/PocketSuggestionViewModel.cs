using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PocketSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private List<string> _labels;

	public PocketSuggestionViewModel(SmartLabel currentLabels)
	{
		_labels = currentLabels.Labels.ToList();
	}
}
