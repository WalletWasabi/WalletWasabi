using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Helpers;

public class ValueList<T>(T[] items) : IEnumerable<T>, IEquatable<ValueList<T>>
	where T : IEquatable<T>
{
	public static ValueList<T> Empty { get; } = new([]);

	public bool Equals(ValueList<T>? other) =>
		this.SequenceEqual(other as IEnumerable<T> ?? []);

	public override int GetHashCode() =>
		items.Aggregate(typeof(T).GetHashCode(), HashCode.Combine);

	public override bool Equals(object? obj) =>
		Equals(obj as ValueList<T>);

	public IEnumerator<T> GetEnumerator() =>
		((IEnumerable<T>) items).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() =>
		((IEnumerable) items).GetEnumerator();
}
