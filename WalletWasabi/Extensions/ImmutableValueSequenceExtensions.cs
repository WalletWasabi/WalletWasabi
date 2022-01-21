using System.Collections.Generic;
using WalletWasabi.Models;

namespace WalletWasabi.Extensions;

public static class ImmutableValueSequenceExtensions
{
	public static ImmutableValueSequence<T> ToImmutableValueSequence<T>(this IEnumerable<T> list) where T : IEquatable<T>
		=> new(list);
}
