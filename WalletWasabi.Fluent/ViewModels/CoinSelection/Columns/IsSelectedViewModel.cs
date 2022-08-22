using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;

public partial class IsSelectedViewModel : ViewModelBase
{
	[AutoNotify] private bool _isSelected;

	public IsSelectedViewModel(bool initialValue, Action<bool> setter)
	{
		IsSelected = initialValue;
		this.WhenAnyValue<IsSelectedViewModel, bool>(model => model.IsSelected).Subscribe(setter);
	}
}
