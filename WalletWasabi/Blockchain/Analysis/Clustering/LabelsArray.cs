using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WalletWasabi.Blockchain.Analysis.Clustering;

public readonly struct LabelsArray : IReadOnlyCollection<string>, IEquatable<LabelsArray>, IComparable<LabelsArray>
{
	private readonly string[]? _labels;

	public LabelsArray(params string[] labels) : this(labels as IEnumerable<string>)
	{
	}

	public LabelsArray(IEnumerable<string>? labels)
	{
		_labels = (labels ?? Array.Empty<string>())
			.SelectMany(x => x?.Split(Separators, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
			.Select(x => x.Trim())
			.Where(x => x.Length != 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	public static LabelsArray Empty { get; } = new();
	public static char[] Separators { get; } = new[] { ',', ':' };

	public int Count => AsSpan().Length;
	public bool IsEmpty => AsSpan().IsEmpty;

	public ReadOnlySpan<string> AsSpan() => _labels;

	public override string ToString()
	{
		const string Separator = ", ";
		int length = 0;

		foreach (var label in this)
		{
			length += label.Length + Separator.Length;
		}

		if (length == 0)
		{
			return string.Empty;
		}

		return string.Create(length - Separator.Length, this, static (span, self) =>
		{
			var index = 0;

			foreach (var label in self)
			{
				label.CopyTo(span[index..]);
				index += label.Length;

				if (index < span.Length)
				{
					Separator.CopyTo(span[index..]);
					index += Separator.Length;
				}
			}
		});
	}

	public override int GetHashCode() => GetHashCode(null);

	public int GetHashCode(IEqualityComparer<string>? comparer)
	{
		var hashCode = new HashCode();

		foreach (var label in this)
		{
			hashCode.Add(label, comparer);
		}

		return hashCode.ToHashCode();
	}

	public override bool Equals(object? other)
	{
		return other is LabelsArray label && label.Equals(this);
	}

	public bool Equals(LabelsArray other) =>
		AsSpan().SequenceEqual(other.AsSpan());

	public bool Equals(LabelsArray other, IEqualityComparer<string>? comparer) =>
		AsSpan().SequenceEqual(other.AsSpan(), comparer);

	public int CompareTo(LabelsArray other) =>
		AsSpan().SequenceCompareTo(other.AsSpan());

	public int CompareTo(LabelsArray other, IComparer<string>? comparer)
	{
		if (comparer is null)
		{
			return CompareTo(other);
		}

		// The following code repeats what .NET could have
		// if SequenceCompareTo is accepted and implemented`.
		var thisLabels = AsSpan();
		var otherLabels = other.AsSpan();

		return CompareToSlow(
			ref MemoryMarshal.GetReference(thisLabels),
			thisLabels.Length,
			ref MemoryMarshal.GetReference(otherLabels),
			otherLabels.Length,
			comparer);

		static int CompareToSlow(ref string first, int firstLength, ref string second, int secondLength, IComparer<string> comparer)
		{
			int minLength = firstLength;
			if (minLength > secondLength)
			{
				minLength = secondLength;
			}

			for (int i = 0; i < minLength; i++)
			{
				int result = comparer.Compare(
					Unsafe.Add(ref first, i),
					Unsafe.Add(ref second, i));

				if (result != 0)
				{
					return result;
				}
			}

			return firstLength.CompareTo(secondLength);
		}
	}

	public ReadOnlySpan<string>.Enumerator GetEnumerator() =>
		AsSpan().GetEnumerator();

	IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumeratorAllocating();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumeratorAllocating();

	private IEnumerator<string> GetEnumeratorAllocating() =>
		(_labels ?? Enumerable.Empty<string>()).GetEnumerator();

	public static LabelsArray Merge(params LabelsArray[] labels) =>
		Merge(labels as IEnumerable<LabelsArray>);

	public static LabelsArray Merge(IEnumerable<LabelsArray> labels) =>
		new(labels.SelectMany(x => x));

	public static bool operator ==(LabelsArray x, LabelsArray y) => x.Equals(y);

	public static bool operator !=(LabelsArray x, LabelsArray y) => !(x == y);

	public static implicit operator LabelsArray(string labels) => new(labels);

	public static implicit operator string(LabelsArray labels) => labels.ToString();
}
