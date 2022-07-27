using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using DynamicData;

namespace WalletWasabi.Tests.UnitTests.Payment;

public static class TestMixin
{
	public static ReadOnlyCollection<T> RecordChanges<T>(this IObservable<T> observable, Action? mutate = default)
	{
		using var x = observable
			.ToObservableChangeSet(ImmediateScheduler.Instance)
			.Bind(out var changes)
			.Subscribe();

		mutate?.Invoke();

		return changes.ToList().AsReadOnly();
	}
}
