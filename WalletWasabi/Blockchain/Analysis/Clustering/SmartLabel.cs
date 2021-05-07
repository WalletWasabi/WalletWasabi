using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.Blockchain.Analysis.Clustering
{
	public class SmartLabel : IEquatable<SmartLabel>, IEquatable<string>, IEnumerable<string>, IComparable<SmartLabel>
	{
		public SmartLabel(params string[] labels) : this(labels as IEnumerable<string>)
		{
		}

		public SmartLabel(IEnumerable<string> labels)
		{
			labels ??= Enumerable.Empty<string>();
			Labels = labels
				   .SelectMany(x => x?.Split(Separators, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
				   .Select(x => x.Trim())
				   .Where(x => x.Length != 0)
				   .Distinct(StringComparer.OrdinalIgnoreCase)
				   .OrderBy(x => x)
				   .ToArray();

			IsEmpty = !Labels.Any();

			LabelString = string.Join(", ", Labels);
		}

		public static SmartLabel Empty { get; } = new SmartLabel();
		public static char[] Separators { get; } = new[] { ',', ':' };
		public IEnumerable<string> Labels { get; }
		public bool IsEmpty { get; }

		private string LabelString { get; }

		public override string ToString() => LabelString;

		public static SmartLabel Merge(IEnumerable<SmartLabel> labels)
		{
			labels ??= Enumerable.Empty<SmartLabel>();

			IEnumerable<string> labelStrings = labels
				.SelectMany(x => x?.Labels ?? Enumerable.Empty<string>())
				.Where(x => x is { });

			return new SmartLabel(labelStrings);
		}

		public static SmartLabel Merge(params SmartLabel[] labels) => Merge(labels as IEnumerable<SmartLabel>);

		public IEnumerator<string> GetEnumerator() => Labels.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		#region Equality

		public int CompareTo(SmartLabel? other)
		{
			if (other is null)
			{
				return -1;
			}

			return string.CompareOrdinal(ToString(), other.ToString());
		}

		public override bool Equals(object? obj) => Equals(obj as SmartLabel) || Equals(obj as string);

		public bool Equals(SmartLabel? other) => this == other;

		public bool Equals(string? other) => this == other;

		public override int GetHashCode() => ((IStructuralEquatable)Labels).GetHashCode(EqualityComparer<string>.Default);

		public static bool operator ==(SmartLabel? x, SmartLabel? y)
		{
			if (x is null)
			{
				if (y is null)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				if (y is null)
				{
					return false;
				}
				else
				{
					return x.Labels.SequenceEqual(y.Labels);
				}
			}
		}

		public static bool operator ==(string? x, SmartLabel? y)
		{
			if (x is null)
			{
				if (y is null)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				if (y is null)
				{
					return false;
				}
				else
				{
					return x == y.LabelString;
				}
			}
		}

		public static bool operator ==(SmartLabel? x, string? y) => y == x;

		public static bool operator !=(SmartLabel? x, SmartLabel? y) => !(x == y);

		public static bool operator !=(string? x, SmartLabel? y) => !(x == y);

		public static bool operator !=(SmartLabel? x, string? y) => !(x == y);

		public static implicit operator SmartLabel(string labels) => new(labels);

		public static implicit operator string(SmartLabel label) => label?.LabelString;

		#endregion Equality
	}
}
