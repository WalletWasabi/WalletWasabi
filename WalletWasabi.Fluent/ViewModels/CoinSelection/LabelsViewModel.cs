using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public class LabelsViewModel : ViewModelBase
{
	public SmartLabel SmartLabel { get; }

	public LabelsViewModel(SmartLabel smartLabel)
	{
		SmartLabel = smartLabel;
	}
}
