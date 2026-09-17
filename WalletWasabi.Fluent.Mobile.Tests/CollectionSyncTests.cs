using System.Collections.ObjectModel;
using System.Collections.Specialized;
using WalletWasabi.Fluent.Mobile.ViewModels;
using Xunit;

namespace WalletWasabi.Fluent.Mobile.Tests;

public sealed class CollectionSyncTests
{
	[Fact]
	public void UnchangedRowsProduceNoNotifications()
	{
		var rows = new[] { new object(), new object(), new object() };
		var current = new ObservableCollection<object>(rows);
		var events = 0;
		current.CollectionChanged += (_, _) => events++;
		MobileCollectionSync.Reconcile(current, rows);
		Assert.Equal(0, events);
	}

	[Fact]
	public void ReorderingRetainsObjectsAndUsesMove()
	{
		var a = new object(); var b = new object(); var c = new object();
		var current = new ObservableCollection<object> { a, b, c };
		var events = new List<NotifyCollectionChangedAction>();
		current.CollectionChanged += (_, e) => events.Add(e.Action);
		MobileCollectionSync.Reconcile(current, new[] { c, a, b });
		Assert.Same(c, current[0]); Assert.Same(a, current[1]); Assert.Same(b, current[2]);
		Assert.Single(events);
		Assert.Equal(NotifyCollectionChangedAction.Move, events[0]);
	}

	[Fact]
	public void EqualValuesDoNotReplaceReferenceIdentity()
	{
		var original = new Row(1);
		var replacement = new Row(1);
		var current = new ObservableCollection<Row> { original };
		MobileCollectionSync.Reconcile(current, new[] { replacement });
		Assert.Same(replacement, current[0]);
	}

	[Fact]
	public void InvalidDuplicateIdentitiesDoNotMutateTheCollection()
	{
		var original = new object();
		var replacement = new object();
		var current = new ObservableCollection<object> { original };
		Assert.Throws<ArgumentException>(() => MobileCollectionSync.Reconcile(current, new[] { replacement, replacement }));
		Assert.Single(current);
		Assert.Same(original, current[0]);
	}

	[Fact]
	public void MixedRefreshesPreserveRequestedOrderWithoutReset()
	{
		var random = new Random(17);
		var all = Enumerable.Range(0, 40).Select(_ => new object()).ToArray();
		var current = new ObservableCollection<object>();
		var actions = new List<NotifyCollectionChangedAction>();
		current.CollectionChanged += (_, e) => actions.Add(e.Action);
		for (var iteration = 0; iteration < 200; iteration++)
		{
			var desired = all.Where(_ => random.Next(2) == 0).OrderBy(_ => random.Next()).ToArray();
			MobileCollectionSync.Reconcile(current, desired);
			Assert.Equal(desired.Length, current.Count);
			for (var index = 0; index < desired.Length; index++) Assert.Same(desired[index], current[index]);
		}
		Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
	}

	private sealed record Row(int Id);
}
