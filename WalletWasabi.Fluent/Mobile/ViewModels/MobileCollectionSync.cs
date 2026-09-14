using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WalletWasabi.Fluent.Mobile.ViewModels;

/// <summary>Applies a reference-identity diff on the UI thread, preserving realized row controls.</summary>
public static class MobileCollectionSync
{
	public static void Reconcile<T>(ObservableCollection<T> current, IReadOnlyList<T> desired) where T : class
	{
		ArgumentNullException.ThrowIfNull(current);
		ArgumentNullException.ThrowIfNull(desired);
		var retained = new HashSet<T>(ReferenceEqualityComparer.Instance);
		foreach (var item in desired)
		{
			if (item is null || !retained.Add(item))
			{
				throw new ArgumentException("Rows must be non-null and have unique identities.", nameof(desired));
			}
		}
		for (var index = current.Count - 1; index >= 0; index--)
		{
			if (!retained.Contains(current[index])) current.RemoveAt(index);
		}
		for (var index = 0; index < desired.Count; index++)
		{
			if (index < current.Count && ReferenceEquals(current[index], desired[index])) continue;
			var previousIndex = -1;
			for (var candidate = index + 1; candidate < current.Count; candidate++)
			{
				if (!ReferenceEquals(current[candidate], desired[index])) continue;
				previousIndex = candidate;
				break;
			}
			if (previousIndex >= 0) current.Move(previousIndex, index);
			else current.Insert(index, desired[index]);
		}
		while (current.Count > desired.Count) current.RemoveAt(current.Count - 1);
	}
}
