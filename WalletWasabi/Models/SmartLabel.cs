using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Models
{
	public class SmartLabel : IEquatable<SmartLabel>
	{
		public static SmartLabel Empty { get; } = new SmartLabel();
		public static char[] Separators { get; } = new[] { ',', ':' };
		public IEnumerable<string> Labels { get; }
		public bool IsEmpty { get; }

		private string LabelString { get; }

		public SmartLabel(params string[] labels) : this(labels as IEnumerable<string>)
		{
		}

		public SmartLabel(IEnumerable<string> labels)
		{
			labels ??= Enumerable.Empty<string>();
			Labels = labels
				   .SelectMany(x => x?.Split(Separators, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
				   .Select(x => x.Trim())
				   .Where(x => x != "")
				   .Distinct(StringComparer.OrdinalIgnoreCase)
				   .OrderBy(x => x)
				   .ToArray();

			HashCode = ((IStructuralEquatable)Labels).GetHashCode(EqualityComparer<string>.Default);
			IsEmpty = !Labels.Any();

			LabelString = string.Join(", ", Labels);
		}

		public override string ToString() => LabelString;

		public static SmartLabel Merge(IEnumerable<SmartLabel> labels)
		{
			labels ??= Enumerable.Empty<SmartLabel>();

			IEnumerable<string> labelStrings = labels
				.SelectMany(x => x?.Labels ?? Enumerable.Empty<string>())
				.Where(x => x != null);

			return new SmartLabel(labelStrings);
		}

		public static SmartLabel Merge(params SmartLabel[] labels) => Merge(labels as IEnumerable<SmartLabel>);

		#region Equality

		public override bool Equals(object obj) => obj is SmartLabel label && this == label;

		public bool Equals(SmartLabel other) => this == other;

		private int HashCode { get; }

		public override int GetHashCode() => HashCode;

		public static bool operator ==(SmartLabel x, SmartLabel y)
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

		public static bool operator !=(SmartLabel x, SmartLabel y) => !(x == y);

		#endregion Equality
	}
}
