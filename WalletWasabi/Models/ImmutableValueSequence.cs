using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.Models;

/// <summary>
/// ImmutableValueSequence represents an immutable sequence with value semantics, meaning that
/// it can be compared with other sequences and the equality of two sequences is based on the
/// equality of the elements.
/// </summary>
public class ImmutableValueSequence<T> : IEnumerable<T>, IEquatable<ImmutableValueSequence<T>> where T : IEquatable<T>
{
	private readonly ImmutableArray<T> _elements;

	public ImmutableValueSequence(IEnumerable<T> sequence)
	{
		_elements = sequence.ToImmutableArray();
	}

	public static ImmutableValueSequence<T> Empty { get; } = new(Enumerable.Empty<T>());

	public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_elements).GetEnumerator();

	public override int GetHashCode()
	{
		var hash = new HashCode();
		foreach (var element in _elements)
		{
			hash.Add(element);
		}
		return hash.ToHashCode();
	}

	public bool Equals(ImmutableValueSequence<T>? other)
		=> this.SequenceEqual(other ?? Enumerable.Empty<T>());

	public override bool Equals(object? obj) => Equals(obj as ImmutableValueSequence<T>);

	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_elements).GetEnumerator();
}
