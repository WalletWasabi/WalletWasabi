using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using DynamicData;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public static class TestHelper
{
	public static ReadOnlyCollection<T> RecordChanges<T>(this IObservable<T> observable, Action mutate)
	{
		using var x = observable
			.ToObservableChangeSet(ImmediateScheduler.Instance)
			.Bind(out var changes)
			.Subscribe();

		mutate();

		return changes.ToList().AsReadOnly();
	}
}
