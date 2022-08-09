using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class LabelsListBox : LabelsItemsPresenter, IStyleable
{
	Type IStyleable.StyleKey => typeof(LabelsListBox);
}
