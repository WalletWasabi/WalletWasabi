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

		public bool Equals([AllowNull] SortingPreference other) => SortOrder == other.SortOrder && ColumnTarget == other.ColumnTarget;
	}
}
