using System.Collections.Generic;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public static class Extensions
{
	public static IEnumerable<bool> SubscribeList(this IObservable<bool> observable)
	{
		var list = new List<bool>();
		observable.Subscribe(list.Add);
		return list;
	}

	public static IDisposable Dump<T>(this IObservable<T> observable, ICollection<T> destination)
	{
		return observable.Subscribe(destination.Add);
	}

	public static void Inject<T>(this IObserver<T> observer, IEnumerable<T> toInject)
	{
		foreach (var v in toInject)
		{
			observer.OnNext(v);
		}
	}
}
