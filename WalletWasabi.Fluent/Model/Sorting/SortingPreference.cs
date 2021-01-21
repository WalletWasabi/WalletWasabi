using System;

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

		#region EqualityAndComparison

		public static bool operator ==(SortingPreference x, SortingPreference y)
			=> (x.SortOrder, x.ColumnTarget) == (y.SortOrder, y.ColumnTarget);

		public static bool operator !=(SortingPreference x, SortingPreference y) => !(x == y);

		public override bool Equals(object? obj)
		{
			if (obj is SortingPreference sp)
			{
				return Equals(sp);
			}
			else
			{
				return false;
			}
		}

		public bool Equals(SortingPreference other) => this == other;

		public override int GetHashCode() => (SortOrder, ColumnTarget).GetHashCode();

		#endregion EqualityAndComparison

		public SortOrder Match(SortOrder targetOrd, string match)
		{
			return ColumnTarget == match ? targetOrd : SortOrder.None;
		}
	}
}
