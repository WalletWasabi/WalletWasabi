using System.Collections.Immutable;

namespace WalletWasabi.EventSourcing.Records
{
	public record AggregateTypeIds(long TailIndex, ImmutableSortedSet<string> Ids)
	{
		/// <summary>
		/// Index of the last aggregateId in this aggregateType
		/// </summary>
		public long TailIndex { get; init; } = TailIndex;

		/// <summary>
		/// List of aggregate Ids in this aggregateType
		/// </summary>
		public ImmutableSortedSet<string> Ids { get; init; } = Ids;
	}
}
