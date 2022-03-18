namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.PreviewWarning;

public abstract class TransactionWarningViewModelBase : ViewModelBase
{
	public abstract string Title { get; }

	public abstract string Message { get; }
}
