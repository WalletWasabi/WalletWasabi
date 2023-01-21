using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using WalletWasabi.Bridge;

namespace WalletWasabi.Fluent.Tests;

public class Tester : IHwiClient
{
	public IObservable<Result> Show(IAddress address)
	{
		return Observable.Return(Result.Failure("Se ha ido al pedo"));
	}
}
