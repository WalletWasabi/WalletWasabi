namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public interface IWorkflowValidator
{
	IObservable<bool> IsValidObservable { get; }
	void Signal(bool isValid);
}
