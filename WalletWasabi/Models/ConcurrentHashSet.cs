using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	/// <summary>
	/// Represents a thread-safe hash-based unique collection.
	/// </summary>
	/// <typeparam name="T">The type of the items in the collection.</typeparam>
	/// <remarks>
	/// All public members of <see cref="ConcurrentHashSet{T}"/> are thread-safe and may be used
	/// concurrently from multiple threads.
	/// https://github.com/i3arnon/ConcurrentHashSet/
	/// </remarks>
	[DebuggerDisplay("Count = {" + nameof(Count) + "}")]
	public class ConcurrentHashSet<T> : IReadOnlyCollection<T>, ICollection<T>
	{
		private const int DefaultCapacity = 31;
		private const int MaxLockNumber = 1024;

		private IEqualityComparer<T> Comparer { get; }
		private bool GrowLockArray { get; }

		private int _budget;
		private volatile Tables _tables;

		private static int DefaultConcurrencyLevel => EnvironmentHelpers.ProcessorCount;

		/// <summary>
		/// Gets the number of items contained in the <see
		/// cref="ConcurrentHashSet{T}"/>.
		/// </summary>
		/// <value>The number of items contained in the <see
		/// cref="ConcurrentHashSet{T}"/>.</value>
		/// <remarks>Count has snapshot semantics and represents the number of items in the <see
		/// cref="ConcurrentHashSet{T}"/>
		/// at the moment when Count was accessed.</remarks>
		public int Count
		{
			get
			{
				var count = 0;
				var acquiredLocks = 0;
				try
				{
					AcquireAllLocks(ref acquiredLocks);

					for (var i = 0; i < _tables.CountPerLock.Length; i++)
					{
						count += _tables.CountPerLock[i];
					}
				}
				finally
				{
					ReleaseLocks(0, acquiredLocks);
				}

				return count;
			}
		}

		/// <summary>
		/// Gets a value that indicates whether the <see cref="ConcurrentHashSet{T}"/> is empty.
		/// </summary>
		/// <value>true if the <see cref="ConcurrentHashSet{T}"/> is empty; otherwise,
		/// false.</value>
		public bool IsEmpty
		{
			get
			{
				var acquiredLocks = 0;
				try
				{
					AcquireAllLocks(ref acquiredLocks);

					for (var i = 0; i < _tables.CountPerLock.Length; i++)
					{
						if (_tables.CountPerLock[i] != 0)
						{
							return false;
						}
					}
				}
				finally
				{
					ReleaseLocks(0, acquiredLocks);
				}

				return true;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see
		/// cref="ConcurrentHashSet{T}"/>
		/// class that is empty, has the default concurrency level, has the default initial capacity, and
		/// uses the default comparer for the item type.
		/// </summary>
		public ConcurrentHashSet()
			: this(DefaultConcurrencyLevel, DefaultCapacity, true, null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see
		/// cref="ConcurrentHashSet{T}"/>
		/// class that is empty, has the specified concurrency level and capacity, and uses the default
		/// comparer for the item type.
		/// </summary>
		/// <param name="concurrencyLevel">The estimated number of threads that will update the
		/// <see cref="ConcurrentHashSet{T}"/> concurrently.</param>
		/// <param name="capacity">The initial number of elements that the <see
		/// cref="ConcurrentHashSet{T}"/>
		/// can contain.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is
		/// less than 1.</exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="capacity"/> is less than
		/// 0.</exception>
		public ConcurrentHashSet(int concurrencyLevel, int capacity)
			: this(concurrencyLevel, capacity, false, null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConcurrentHashSet{T}"/>
		/// class that contains elements copied from the specified <see
		/// cref="T:System.Collections.IEnumerable{T}"/>, has the default concurrency
		/// level, has the default initial capacity, and uses the default comparer for the item type.
		/// </summary>
		/// <param name="collection">The <see
		/// cref="T:System.Collections.IEnumerable{T}"/> whose elements are copied to
		/// the new
		/// <see cref="ConcurrentHashSet{T}"/>.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="collection"/> is a null reference.</exception>
		public ConcurrentHashSet(IEnumerable<T> collection)
			: this(collection, null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConcurrentHashSet{T}"/>
		/// class that is empty, has the specified concurrency level and capacity, and uses the specified
		/// <see cref="T:System.Collections.Generic.IEqualityComparer{T}"/>.
		/// </summary>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer{T}"/>
		/// implementation to use when comparing items.</param>
		public ConcurrentHashSet(IEqualityComparer<T> comparer)
			: this(DefaultConcurrencyLevel, DefaultCapacity, true, comparer)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConcurrentHashSet{T}"/>
		/// class that contains elements copied from the specified <see
		/// cref="T:System.Collections.IEnumerable"/>, has the default concurrency level, has the default
		/// initial capacity, and uses the specified
		/// <see cref="T:System.Collections.Generic.IEqualityComparer{T}"/>.
		/// </summary>
		/// <param name="collection">The <see
		/// cref="T:System.Collections.IEnumerable{T}"/> whose elements are copied to
		/// the new
		/// <see cref="ConcurrentHashSet{T}"/>.</param>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer{T}"/>
		/// implementation to use when comparing items.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="collection"/> is a null reference
		/// (Nothing in Visual Basic).
		/// </exception>
		public ConcurrentHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer)
			: this(comparer)
		{
			if (collection is null)
			{
				throw new ArgumentNullException(nameof(collection));
			}

			InitializeFromCollection(collection);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConcurrentHashSet{T}"/>
		/// class that contains elements copied from the specified <see cref="T:System.Collections.IEnumerable"/>,
		/// has the specified concurrency level, has the specified initial capacity, and uses the specified
		/// <see cref="T:System.Collections.Generic.IEqualityComparer{T}"/>.
		/// </summary>
		/// <param name="concurrencyLevel">The estimated number of threads that will update the
		/// <see cref="ConcurrentHashSet{T}"/> concurrently.</param>
		/// <param name="collection">The <see cref="T:System.Collections.IEnumerable{T}"/> whose elements are copied to the new
		/// <see cref="ConcurrentHashSet{T}"/>.</param>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer{T}"/> implementation to use
		/// when comparing items.</param>
		/// <exception cref="T:System.ArgumentNullException">
		/// <paramref name="collection"/> is a null reference.
		/// </exception>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="concurrencyLevel"/> is less than 1.
		/// </exception>
		public ConcurrentHashSet(int concurrencyLevel, IEnumerable<T> collection, IEqualityComparer<T> comparer)
			: this(concurrencyLevel, DefaultCapacity, false, comparer)
		{
			if (collection is null)
			{
				throw new ArgumentNullException(nameof(collection));
			}

			InitializeFromCollection(collection);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConcurrentHashSet{T}"/>
		/// class that is empty, has the specified concurrency level, has the specified initial capacity, and
		/// uses the specified <see cref="T:System.Collections.Generic.IEqualityComparer{T}"/>.
		/// </summary>
		/// <param name="concurrencyLevel">The estimated number of threads that will update the
		/// <see cref="ConcurrentHashSet{T}"/> concurrently.</param>
		/// <param name="capacity">The initial number of elements that the <see
		/// cref="ConcurrentHashSet{T}"/>
		/// can contain.</param>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer{T}"/>
		/// implementation to use when comparing items.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="concurrencyLevel"/> is less than 1. -or-
		/// <paramref name="capacity"/> is less than 0.
		/// </exception>
		public ConcurrentHashSet(int concurrencyLevel, int capacity, IEqualityComparer<T> comparer)
			: this(concurrencyLevel, capacity, false, comparer)
		{
		}

		private ConcurrentHashSet(int concurrencyLevel, int capacity, bool growLockArray, IEqualityComparer<T> comparer)
		{
			if (concurrencyLevel < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));
			}

			if (capacity < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(capacity));
			}

			// The capacity should be at least as large as the concurrency level. Otherwise, we would have locks that don't guard
			// any buckets.
			if (capacity < concurrencyLevel)
			{
				capacity = concurrencyLevel;
			}

			var locks = new object[concurrencyLevel];
			for (var i = 0; i < locks.Length; i++)
			{
				locks[i] = new object();
			}

			var countPerLock = new int[locks.Length];
			var buckets = new Node[capacity];
			_tables = new Tables(buckets, locks, countPerLock);

			GrowLockArray = growLockArray;
			_budget = buckets.Length / locks.Length;
			Comparer = comparer ?? EqualityComparer<T>.Default;
		}

		/// <summary>
		/// Adds the specified item to the <see cref="ConcurrentHashSet{T}"/>.
		/// </summary>
		/// <param name="item">The item to add.</param>
		/// <returns>true if the items was added to the <see cref="ConcurrentHashSet{T}"/>
		/// successfully; false if it already exists.</returns>
		/// <exception cref="T:System.OverflowException">The <see cref="ConcurrentHashSet{T}"/>
		/// contains too many items.</exception>
		public bool TryAdd(T item) =>
			AddInternal(item, Comparer.GetHashCode(item), true);

		/// <summary>
		/// Removes all items from the <see cref="ConcurrentHashSet{T}"/>.
		/// </summary>
		public void Clear()
		{
			var locksAcquired = 0;
			try
			{
				AcquireAllLocks(ref locksAcquired);

				var newTables = new Tables(new Node[DefaultCapacity], _tables.Locks, new int[_tables.CountPerLock.Length]);
				_tables = newTables;
				_budget = Math.Max(1, newTables.Buckets.Length / newTables.Locks.Length);
			}
			finally
			{
				ReleaseLocks(0, locksAcquired);
			}
		}

		/// <summary>
		/// Determines whether the <see cref="ConcurrentHashSet{T}"/> contains the specified
		/// item.
		/// </summary>
		/// <param name="item">The item to locate in the <see cref="ConcurrentHashSet{T}"/>.</param>
		/// <returns>true if the <see cref="ConcurrentHashSet{T}"/> contains the item; otherwise, false.</returns>
		public bool Contains(T item)
		{
			var hashcode = Comparer.GetHashCode(item);

			// We must capture the _buckets field in a local variable. It is set to a new table on each table resize.
			var tables = _tables;

			var bucketNo = GetBucket(hashcode, tables.Buckets.Length);

			// We can get away w/out a lock here.
			// The Volatile.Read ensures that the load of the fields of 'n' doesn't move before the load from buckets[i].
			var current = Volatile.Read(ref tables.Buckets[bucketNo]);

			while (current != null)
			{
				if (hashcode == current.Hashcode && Comparer.Equals(current.Item, item))
				{
					return true;
				}
				current = current.Next;
			}

			return false;
		}

		/// <summary>
		/// Attempts to remove the item from the <see cref="ConcurrentHashSet{T}"/>.
		/// </summary>
		/// <param name="item">The item to remove.</param>
		/// <returns>true if an item was removed successfully; otherwise, false.</returns>
		public bool TryRemove(T item)
		{
			var hashcode = Comparer.GetHashCode(item);
			while (true)
			{
				var tables = _tables;

				GetBucketAndLockNo(hashcode, out int bucketNo, out int lockNo, tables.Buckets.Length, tables.Locks.Length);

				lock (tables.Locks[lockNo])
				{
					// If the table just got resized, we may not be holding the right lock, and must retry.
					// This should be a rare occurrence.
					if (tables != _tables)
					{
						continue;
					}

					Node previous = null;
					for (var current = tables.Buckets[bucketNo]; current != null; current = current.Next)
					{
						Debug.Assert((previous is null && current == tables.Buckets[bucketNo]) || previous.Next == current);

						if (hashcode == current.Hashcode && Comparer.Equals(current.Item, item))
						{
							if (previous is null)
							{
								Volatile.Write(ref tables.Buckets[bucketNo], current.Next);
							}
							else
							{
								previous.Next = current.Next;
							}

							tables.CountPerLock[lockNo]--;
							return true;
						}
						previous = current;
					}
				}

				return false;
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <summary>Returns an enumerator that iterates through the <see
		/// cref="ConcurrentHashSet{T}"/>.</summary>
		/// <returns>An enumerator for the <see cref="ConcurrentHashSet{T}"/>.</returns>
		/// <remarks>
		/// The enumerator returned from the collection is safe to use concurrently with
		/// reads and writes to the collection, however it does not represent a moment-in-time snapshot
		/// of the collection.  The contents exposed through the enumerator may contain modifications
		/// made to the collection after <see cref="GetEnumerator"/> was called.
		/// </remarks>
		public IEnumerator<T> GetEnumerator()
		{
			var buckets = _tables.Buckets;

			for (var i = 0; i < buckets.Length; i++)
			{
				// The Volatile.Read ensures that the load of the fields of 'current' doesn't move before the load from buckets[i].
				var current = Volatile.Read(ref buckets[i]);

				while (current != null)
				{
					yield return current.Item;
					current = current.Next;
				}
			}
		}

		void ICollection<T>.Add(T item) => TryAdd(item);

		bool ICollection<T>.IsReadOnly => false;

		void ICollection<T>.CopyTo(T[] array, int arrayIndex)
		{
			if (array is null)
			{
				throw new ArgumentNullException(nameof(array));
			}

			if (arrayIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(arrayIndex));
			}

			var locksAcquired = 0;
			try
			{
				AcquireAllLocks(ref locksAcquired);

				var count = 0;

				for (var i = 0; i < _tables.Locks.Length && count >= 0; i++)
				{
					count += _tables.CountPerLock[i];
				}

				if (array.Length - count < arrayIndex || count < 0) //"count" itself or "count + arrayIndex" can overflow
				{
					throw new ArgumentException("The index is equal to or greater than the length of the array, or the number of elements in the set is greater than the available space from index to the end of the destination array.");
				}

				CopyToItems(array, arrayIndex);
			}
			finally
			{
				ReleaseLocks(0, locksAcquired);
			}
		}

		bool ICollection<T>.Remove(T item) => TryRemove(item);

		private void InitializeFromCollection(IEnumerable<T> collection)
		{
			foreach (var item in collection)
			{
				AddInternal(item, Comparer.GetHashCode(item), false);
			}

			if (_budget == 0)
			{
				_budget = _tables.Buckets.Length / _tables.Locks.Length;
			}
		}

		private bool AddInternal(T item, int hashcode, bool acquireLock)
		{
			while (true)
			{
				var tables = _tables;

				GetBucketAndLockNo(hashcode, out int bucketNo, out int lockNo, tables.Buckets.Length, tables.Locks.Length);

				var resizeDesired = false;
				var lockTaken = false;
				try
				{
					if (acquireLock)
					{
						Monitor.Enter(tables.Locks[lockNo], ref lockTaken);
					}

					// If the table just got resized, we may not be holding the right lock, and must retry.
					// This should be a rare occurrence.
					if (tables != _tables)
					{
						continue;
					}

					// Try to find this item in the bucket
					Node previous = null;
					for (var current = tables.Buckets[bucketNo]; current != null; current = current.Next)
					{
						Debug.Assert((previous is null && current == tables.Buckets[bucketNo]) || previous.Next == current);
						if (hashcode == current.Hashcode && Comparer.Equals(current.Item, item))
						{
							return false;
						}
						previous = current;
					}

					// The item was not found in the bucket. Insert the new item.
					Volatile.Write(ref tables.Buckets[bucketNo], new Node(item, hashcode, tables.Buckets[bucketNo]));
					checked
					{
						tables.CountPerLock[lockNo]++;
					}

					//
					// If the number of elements guarded by this lock has exceeded the budget, resize the bucket table.
					// It is also possible that GrowTable will increase the budget but won't resize the bucket table.
					// That happens if the bucket table is found to be poorly utilized due to a bad hash function.
					//
					if (tables.CountPerLock[lockNo] > _budget)
					{
						resizeDesired = true;
					}
				}
				finally
				{
					if (lockTaken)
					{
						Monitor.Exit(tables.Locks[lockNo]);
					}
				}

				//
				// The fact that we got here means that we just performed an insertion. If necessary, we will grow the table.
				//
				// Concurrency notes:
				// - Notice that we are not holding any locks at when calling GrowTable. This is necessary to prevent deadlocks.
				// - As a result, it is possible that GrowTable will be called unnecessarily. But, GrowTable will obtain lock 0
				//   and then verify that the table we passed to it as the argument is still the current table.
				//
				if (resizeDesired)
				{
					GrowTable(tables);
				}

				return true;
			}
		}

		private static int GetBucket(int hashcode, int bucketCount)
		{
			var bucketNo = (hashcode & 0x7fffffff) % bucketCount;
			Debug.Assert(bucketNo >= 0 && bucketNo < bucketCount);
			return bucketNo;
		}

		private static void GetBucketAndLockNo(int hashcode, out int bucketNo, out int lockNo, int bucketCount, int lockCount)
		{
			bucketNo = (hashcode & 0x7fffffff) % bucketCount;
			lockNo = bucketNo % lockCount;

			Debug.Assert(bucketNo >= 0 && bucketNo < bucketCount);
			Debug.Assert(lockNo >= 0 && lockNo < lockCount);
		}

		private void GrowTable(Tables tables)
		{
			const int maxArrayLength = 0X7FEFFFFF;
			var locksAcquired = 0;
			try
			{
				// The thread that first obtains _locks[0] will be the one doing the resize operation
				AcquireLocks(0, 1, ref locksAcquired);

				// Make sure nobody resized the table while we were waiting for lock 0:
				if (tables != _tables)
				{
					// We assume that since the table reference is different, it was already resized (or the budget
					// was adjusted). If we ever decide to do table shrinking, or replace the table for other reasons,
					// we will have to revisit this logic.
					return;
				}

				// Compute the (approx.) total size. Use an Int64 accumulation variable to avoid an overflow.
				long approxCount = 0;
				for (var i = 0; i < tables.CountPerLock.Length; i++)
				{
					approxCount += tables.CountPerLock[i];
				}

				//
				// If the bucket array is too empty, double the budget instead of resizing the table
				//
				if (approxCount < tables.Buckets.Length / 4)
				{
					_budget = 2 * _budget;
					if (_budget < 0)
					{
						_budget = int.MaxValue;
					}
					return;
				}

				// Compute the new table size. We find the smallest integer larger than twice the previous table size, and not divisible by
				// 2,3,5 or 7. We can consider a different table-sizing policy in the future.
				var newLength = 0;
				var maximizeTableSize = false;
				try
				{
					checked
					{
						// Double the size of the buckets table and add one, so that we have an odd integer.
						newLength = (tables.Buckets.Length * 2) + 1;

						// Now, we only need to check odd integers, and find the first that is not divisible
						// by 3, 5 or 7.
						while (newLength % 3 == 0 || newLength % 5 == 0 || newLength % 7 == 0)
						{
							newLength += 2;
						}

						Debug.Assert(newLength % 2 != 0);

						if (newLength > maxArrayLength)
						{
							maximizeTableSize = true;
						}
					}
				}
				catch (OverflowException)
				{
					maximizeTableSize = true;
				}

				if (maximizeTableSize)
				{
					newLength = maxArrayLength;

					// We want to make sure that GrowTable will not be called again, since table is at the maximum size.
					// To achieve that, we set the budget to int.MaxValue.
					//
					// (There is one special case that would allow GrowTable() to be called in the future:
					// calling Clear() on the ConcurrentHashSet will shrink the table and lower the budget.)
					_budget = int.MaxValue;
				}

				// Now acquire all other locks for the table
				AcquireLocks(1, tables.Locks.Length, ref locksAcquired);

				var newLocks = tables.Locks;

				// Add more locks
				if (GrowLockArray && tables.Locks.Length < MaxLockNumber)
				{
					newLocks = new object[tables.Locks.Length * 2];
					Array.Copy(tables.Locks, 0, newLocks, 0, tables.Locks.Length);
					for (var i = tables.Locks.Length; i < newLocks.Length; i++)
					{
						newLocks[i] = new object();
					}
				}

				var newBuckets = new Node[newLength];
				var newCountPerLock = new int[newLocks.Length];

				// Copy all data into a new table, creating new nodes for all elements
				for (var i = 0; i < tables.Buckets.Length; i++)
				{
					var current = tables.Buckets[i];
					while (current != null)
					{
						var next = current.Next;
						GetBucketAndLockNo(current.Hashcode, out int newBucketNo, out int newLockNo, newBuckets.Length, newLocks.Length);

						newBuckets[newBucketNo] = new Node(current.Item, current.Hashcode, newBuckets[newBucketNo]);

						checked
						{
							newCountPerLock[newLockNo]++;
						}

						current = next;
					}
				}

				// Adjust the budget
				_budget = Math.Max(1, newBuckets.Length / newLocks.Length);

				// Replace tables with the new versions
				_tables = new Tables(newBuckets, newLocks, newCountPerLock);
			}
			finally
			{
				// Release all locks that we took earlier
				ReleaseLocks(0, locksAcquired);
			}
		}

		private void AcquireAllLocks(ref int locksAcquired)
		{
			// First, acquire lock 0
			AcquireLocks(0, 1, ref locksAcquired);

			// Now that we have lock 0, the _locks array will not change (i.e., grow),
			// and so we can safely read _locks.Length.
			AcquireLocks(1, _tables.Locks.Length, ref locksAcquired);
			Debug.Assert(locksAcquired == _tables.Locks.Length);
		}

		private void AcquireLocks(int fromInclusive, int toExclusive, ref int locksAcquired)
		{
			Debug.Assert(fromInclusive <= toExclusive);
			var locks = _tables.Locks;

			for (var i = fromInclusive; i < toExclusive; i++)
			{
				var lockTaken = false;
				try
				{
					Monitor.Enter(locks[i], ref lockTaken);
				}
				finally
				{
					if (lockTaken)
					{
						locksAcquired++;
					}
				}
			}
		}

		private void ReleaseLocks(int fromInclusive, int toExclusive)
		{
			Debug.Assert(fromInclusive <= toExclusive);

			for (var i = fromInclusive; i < toExclusive; i++)
			{
				Monitor.Exit(_tables.Locks[i]);
			}
		}

		private void CopyToItems(T[] array, int index)
		{
			var buckets = _tables.Buckets;
			for (var i = 0; i < buckets.Length; i++)
			{
				for (var current = buckets[i]; current != null; current = current.Next)
				{
					array[index] = current.Item;
					index++; //this should never flow, CopyToItems is only called when there's no overflow risk
				}
			}
		}

		private class Tables
		{
			public readonly Node[] Buckets;
			public readonly object[] Locks;

			public volatile int[] CountPerLock;

			public Tables(Node[] buckets, object[] locks, int[] countPerLock)
			{
				Buckets = buckets;
				Locks = locks;
				CountPerLock = countPerLock;
			}
		}

		private class Node
		{
			public readonly T Item;
			public readonly int Hashcode;

			public volatile Node Next;

			public Node(T item, int hashcode, Node next)
			{
				Item = item;
				Hashcode = hashcode;
				Next = next;
			}
		}
	}
}
