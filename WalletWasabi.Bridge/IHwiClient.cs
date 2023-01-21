using CSharpFunctionalExtensions;

namespace WalletWasabi.Bridge;

public interface IHwiClient
{
	public IObservable<Result> Show(IAddress address);
}
