using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public class LabelsCellViewModel : ViewModelBase
{
	public SmartLabel SmartLabel { get; }

	public LabelsCellViewModel(SmartLabel smartLabel)
	{
		SmartLabel = smartLabel;
	}
}
