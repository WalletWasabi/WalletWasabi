namespace WalletWasabi.Fluent.Features.Onboarding;

public interface IWizardPage : IValid
{
	public string NextText { get; }
	public bool ShowNext { get; }
}
