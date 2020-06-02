using System;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Gui.Models.Sorting
{
	public struct SortingPreference : IEquatable<SortingPreference>
	{
		public SortingPreference(SortOrder sortOrder, string colTarget)
		{
			SortOrder = sortOrder;
			ColumnTarget = colTarget;
		}

		public SortOrder SortOrder { get; set; }
		public string ColumnTarget { get; set; }

		public SortOrder Match(SortOrder targetOrd, string match)
		{
			return ColumnTarget == match ? targetOrd : SortOrder.None;
		}

		#region EqualityAndComparison

		public override bool Equals(object obj) => Equals(obj as SortingPreference?);

		public bool Equals(SortingPreference other) => this == other;

		public override int GetHashCode() => (SortOrder, ColumnTarget).GetHashCode();

		public static bool operator ==(SortingPreference x, SortingPreference y)
			=> (x.SortOrder, x.ColumnTarget) == (y.SortOrder, y.ColumnTarget);

		public static bool operator !=(SortingPreference x, SortingPreference y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
