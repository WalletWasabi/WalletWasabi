namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public abstract partial class SuggestionViewModel : ViewModelBase
{
	[AutoNotify] private bool _isEnabled = true;
}
