namespace WalletWasabi.Fluent.ViewModels.Wallets.Send.PreviewWarning;

public abstract class TransactionWarningViewModelBase : ViewModelBase
{
	protected TransactionWarningViewModelBase(string title, string message)
	{
		Title = title;
		Message = message;
	}

	public string Title { get; }

	public string Message { get; }
}
