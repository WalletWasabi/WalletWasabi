//from https://github.com/brianchance/MonoTouchMVVMCrossValidationTester/blob/master/Validation.Core/ConcurrentObservableDictionary.cs
//modified

using System.Collections.Concurrent;
using System.Linq;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace System.Collections.ObjectModel
{
	public class ConcurrentObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged
	{
		private const string CountString = "Count";
		private const string IndexerName = "Item[]";
		private const string KeysName = "Keys";
		private const string ValuesName = "Values";

		private readonly object Lock = new object();

		protected ConcurrentDictionary<TKey, TValue> ConcurrentDictionary { get; private set; }

		#region Constructors
		public ConcurrentObservableDictionary()
		{
			ConcurrentDictionary = new ConcurrentDictionary<TKey, TValue>();
		}
		public ConcurrentObservableDictionary(ConcurrentDictionary<TKey, TValue> dictionary)
		{
			ConcurrentDictionary = new ConcurrentDictionary<TKey, TValue>(dictionary);
		}
		public ConcurrentObservableDictionary(IEqualityComparer<TKey> comparer)
		{
			ConcurrentDictionary = new ConcurrentDictionary<TKey, TValue>(comparer);
		}
		public ConcurrentObservableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
		{
			ConcurrentDictionary = new ConcurrentDictionary<TKey, TValue>(dictionary, comparer);
		}
		#endregion

		#region IDictionary<TKey,TValue> Members

		public void Add(TKey key, TValue value) => Insert(key, value, true);

		public bool ContainsKey(TKey key) => ConcurrentDictionary.ContainsKey(key);

		public ICollection<TKey> Keys => ConcurrentDictionary.Keys;

		public bool Remove(TKey key) => Remove(key, suppressNotifications: false);

		private bool Remove(TKey key, bool suppressNotifications)
		{
			lock(Lock)
			{
                var ret = ConcurrentDictionary.TryRemove(key, out TValue value);
                if (ret && !suppressNotifications) OnCollectionChanged();
				return ret;
			}
		}

		public bool TryGetValue(TKey key, out TValue value) => ConcurrentDictionary.TryGetValue(key, out value);

		public ICollection<TValue> Values => ConcurrentDictionary.Values;

		public TValue this[TKey key]
		{
			get
			{
                return TryGetValue(key, out TValue value) ? value : default(TValue);
            }
			set
			{
				Insert(key, value, false);
			}
		}

		#endregion

		#region ICollection<KeyValuePair<TKey,TValue>> Members

		public void Add(KeyValuePair<TKey, TValue> item) => Insert(item.Key, item.Value, true);

		public void Clear()
		{
			lock(Lock)
			{
				if (ConcurrentDictionary.Count > 0)
				{
					ConcurrentDictionary.Clear();
					OnCollectionChanged();
				}
			}
		}

		public bool Contains(KeyValuePair<TKey, TValue> item) => ConcurrentDictionary.Contains(item);

        /// <summary>
        /// NotSupportedException
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => throw new NotSupportedException();

        /// <summary>
        /// NotSupportedException
        /// </summary>
        public bool IsReadOnly => throw new NotSupportedException();

        public int Count => ConcurrentDictionary.Count;

		public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

		#endregion

		#region IEnumerable<KeyValuePair<TKey,TValue>> Members

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => ConcurrentDictionary.GetEnumerator();

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)ConcurrentDictionary).GetEnumerator();

		#endregion

		#region INotifyCollectionChanged Members

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		#endregion

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		public void AddOrReplace(TKey key, TValue value)
		{
			if (ContainsKey(key))
			{
				Remove(key, suppressNotifications: true);
				Add(key, value);
			}
			else
			{
				Add(key, value);
			}
		}

		/// <summary>
		/// NotImplementedException
		/// </summary>
		/// <param name="items"></param>
		public void AddRange(IDictionary<TKey, TValue> items)
		{
			throw new NotImplementedException();
		}

		private void Insert(TKey key, TValue value, bool add)
		{
			lock(Lock)
			{
				if (key == null) throw new ArgumentNullException(nameof(key));

                if (ConcurrentDictionary.TryGetValue(key, out TValue item))
                {
                    if (add) throw new ArgumentException("An item with the same key has already been added.");
                    if (Equals(item, value)) return;
                    ConcurrentDictionary[key] = value;

                    OnCollectionChanged(NotifyCollectionChangedAction.Replace, new KeyValuePair<TKey, TValue>(key, value), new KeyValuePair<TKey, TValue>(key, item));
                    OnPropertyChanged(key.ToString());
                }
                else
                {
                    ConcurrentDictionary[key] = value;

                    OnCollectionChanged(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>(key, value));
                    OnPropertyChanged(key.ToString());
                }
            }
		}

		private void OnPropertyChanged()
		{
			OnPropertyChanged(CountString);
			OnPropertyChanged(IndexerName);
			OnPropertyChanged(KeysName);
			OnPropertyChanged(ValuesName);
		}

		protected virtual void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void OnCollectionChanged()
		{
			OnPropertyChanged();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		private void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue> changedItem)
		{
			OnPropertyChanged();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, changedItem, 0));
		}

		private void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue> newItem, KeyValuePair<TKey, TValue> oldItem)
		{
			OnPropertyChanged();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, newItem, oldItem, 0));
		}
	}
}