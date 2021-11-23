using System;
using System.Collections.Generic;
using WalletWasabi.Models;

namespace WalletWasabi.Extensions
{
	internal static class ImmutableValueSequenceExtensions
	{
		public static ImmutableValueSequence<T> ToImmutableValueSequence<T>(this IEnumerable<T> list) where T : IEquatable<T>
			=> new(list);
	}
}
