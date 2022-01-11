using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing;

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
