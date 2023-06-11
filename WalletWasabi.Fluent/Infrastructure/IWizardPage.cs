namespace WalletWasabi.Fluent.Infrastructure;

public interface IWizardPage : IValid
{
	public string NextText { get; }
	public bool ShowNext { get; }
}
