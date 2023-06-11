namespace WalletWasabi.Fluent.Infrastructure;

public interface IValid
{
	IObservable<bool> IsValid { get; }
}
