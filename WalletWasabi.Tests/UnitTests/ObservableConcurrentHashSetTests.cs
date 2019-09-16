using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class ObservableConcurrentHashSetTests
	{
		private int Set_CollectionChangedInvokeCount { get; set; }
		private object Set_CollectionChangedLock { get; set; }

		[Fact]
		public void ObservableConcurrentHashSetTest()
		{
			Set_CollectionChangedLock = new object();
			Set_CollectionChangedInvokeCount = 0;
			var set = new ObservableConcurrentHashSet<int>();

			set.CollectionChanged += Set_CollectionChanged;
			try
			{
				// CollectionChanged fire 1
				set.TryAdd(1);
				Assert.Contains(1, set);
				Assert.Single(set);

				// CollectionChanged do not fire
				set.TryAdd(1);
				Assert.Contains(1, set);
				Assert.Single(set);

				// CollectionChanged do not fire
				set.TryRemove(2);
				Assert.Single(set);

				// CollectionChanged fire 2
				set.TryAdd(2);
				Assert.Contains(2, set);
				Assert.Equal(2, set.Count);

				// CollectionChanged fire 3
				set.TryRemove(2);
				Assert.Contains(1, set);
				Assert.DoesNotContain(2, set);
				Assert.Single(set);

				// CollectionChanged fire 4
				set.TryAdd(3);
				Assert.Contains(1, set);
				Assert.Contains(3, set);
				Assert.Equal(2, set.Count);

				// CollectionChanged fire 5
				set.Clear();
				Assert.NotNull(set);
				Assert.Empty(set);
			}
			finally
			{
				set.CollectionChanged -= Set_CollectionChanged;
			}
		}

		private void Set_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			lock (Set_CollectionChangedLock)
			{
				Set_CollectionChangedInvokeCount++;

				switch (Set_CollectionChangedInvokeCount)
				{
					case 1:
						{
							Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
							Assert.Single(e.NewItems);
							Assert.Null(e.OldItems);
							Assert.Equal(1, e.NewItems[0]);
							break;
						}
					case 2:
						{
							Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
							Assert.Single(e.NewItems);
							Assert.Null(e.OldItems);
							Assert.Equal(2, e.NewItems[0]);
							break;
						}
					case 3:
						{
							Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
							Assert.Null(e.NewItems);
							Assert.Single(e.OldItems);
							Assert.Equal(2, e.OldItems[0]);
							break;
						}
					case 4:
						{
							Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
							Assert.Single(e.NewItems);
							Assert.Null(e.OldItems);
							Assert.Equal(3, e.NewItems[0]);
							break;
						}
					case 5:
						{
							Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
							Assert.Null(e.NewItems);
							Assert.Null(e.OldItems); // "Reset action must be initialized with no changed items."
							break;
						}
					default:
						throw new NotSupportedException();
				}
			}
		}
	}
}
