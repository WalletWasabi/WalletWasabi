namespace WalletWasabi.Fluent.Features.Onboarding;

public interface IValid
{
	IObservable<bool> IsValid { get; }
}