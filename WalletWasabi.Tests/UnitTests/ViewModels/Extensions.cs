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
}
