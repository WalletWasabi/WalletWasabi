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

		public Label(IEnumerable<string> labels) : this(labels?.ToArray())
		{
		}

		public Label(params string[] labels)
		{
			Labels = (labels
				?.SelectMany(x => x?.Split(Separators, StringSplitOptions.RemoveEmptyEntries) ?? new string[0])
				?.Select(x => x.Trim())
				?.Where(x => x != "")
				?.OrderBy(x => x)
				?? Enumerable.Empty<string>())
				.ToArray();

			HashCode = ((IStructuralEquatable)Labels).GetHashCode(EqualityComparer<string>.Default);
			IsEmpty = !Labels.Any();
		}

		public override string ToString()
		{
			return IsEmpty ? "" : string.Join(", ", Labels);
		}

		#region Equality

		public override bool Equals(object obj) => obj is Label label && this == label;

		public bool Equals(Label other) => this == other;

		private int HashCode { get; }

		public override int GetHashCode() => HashCode;

		public static bool operator ==(Label x, Label y)
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

		public static bool operator !=(Label x, Label y) => !(x == y);

		#endregion Equality
	}
}
