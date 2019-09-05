using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Models
{
	public class Label : IEquatable<Label>
	{
		public static Label Empty { get; } = new Label();
		public static char[] Separators { get; } = new[] { ',', ':' };
		public IEnumerable<string> Labels { get; }
		public bool IsEmpty { get; }

		private string LabelString { get; }

		public Label(params string[] labels) : this(labels as IEnumerable<string>)
		{
		}

		public Label(IEnumerable<string> labels)
		{
			labels = labels ?? Enumerable.Empty<string>();
			Labels = labels
				   .SelectMany(x => x?.Split(Separators, StringSplitOptions.RemoveEmptyEntries) ?? new string[0])
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

		public static Label Merge(IEnumerable<Label> labels)
		{
			IEnumerable<string> labelStrings = labels
				?.SelectMany(x => x?.Labels ?? Enumerable.Empty<string>())
				?.Where(x => x != null);

			return new Label(labelStrings);
		}

		public static Label Merge(params Label[] labels) => Merge(labels as IEnumerable<Label>);

		#region Equality

		public override bool Equals(object obj) => obj is Label label && this == label;

		public bool Equals(Label other) => this == other;

		private int HashCode { get; }

		public override int GetHashCode() => HashCode;

		public static bool operator ==(Label x, Label y)
		{
			if (x is null && y is null)
			{
				return true;
			}

			if (x is null && y != null)
			{
				return false;
			}

			if (x != null && y is null)
			{
				return false;
			}

			return x.Labels.SequenceEqual(y.Labels);
		}

		public static bool operator !=(Label x, Label y) => !(x == y);

		#endregion Equality
	}
}
