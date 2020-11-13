using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Bases;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.Analysis.Clustering
{
	public class Cluster : NotifyPropertyChangedBase, IEquatable<Cluster>
	{
		private SmartLabel _labels;

		public Cluster(params HdPubKey[] keys)
			: this(keys as IEnumerable<HdPubKey>)
		{
		}

		public Cluster(IEnumerable<HdPubKey> keys)
		{
			Lock = new object();
			Keys = keys.ToList();
			KeysSet = Keys.ToHashSet();
			_labels = SmartLabel.Merge(Keys.Select(x => x.Label));
		}

		public SmartLabel Labels
		{
			get => _labels;
			private set => RaiseAndSetIfChanged(ref _labels, value);
		}

		private object Lock { get; }
		private List<HdPubKey> Keys { get; set; }
		private HashSet<HdPubKey> KeysSet { get; set; }

		public void Merge(Cluster cluster) => Merge(cluster.Keys);

		public void Merge(IEnumerable<HdPubKey> keys)
		{
			lock (Lock)
			{
				var insertPosition = 0;
				foreach (var key in keys.ToList())
				{
					if (KeysSet.Add(key))
					{
						Keys.Insert(insertPosition++, key);
					}
					key.Cluster = this;
				}
				if (insertPosition > 0) // at least one element was inserted
				{
					UpdateLabelsNoLock();
				}
			}
		}

		public void UpdateLabels()
		{
			lock (Lock)
			{
				UpdateLabelsNoLock();
			}
		}

		private void UpdateLabelsNoLock()
		{
			Labels = SmartLabel.Merge(Keys.Select(x => x.Label));
		}

		public override string ToString() => Labels;

		#region EqualityAndComparison

		public override bool Equals(object? obj) => Equals(obj as Cluster);

		public bool Equals(Cluster? other) => this == other;

		public override int GetHashCode()
		{
			lock (Lock)
			{
				int hash = 0;
				if (Keys is { })
				{
					foreach (var key in Keys)
					{
						hash ^= key.GetHashCode();
					}
				}
				return hash;
			}
		}

		public static bool operator ==(Cluster? x, Cluster? y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}
			else if (x is null || y is null)
			{
				return false;
			}
			else
			{
				lock (x.Lock)
				{
					lock (y.Lock)
					{
						// We lose the order here, which isn't great and may cause problems,
						// but this is also a significant perfomance gain.
						return x.KeysSet.SetEquals(y.KeysSet);
					}
				}
			}
		}

		public static bool operator !=(Cluster? x, Cluster? y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
