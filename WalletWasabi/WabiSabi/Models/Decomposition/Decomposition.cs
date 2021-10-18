using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	public sealed record Decomposition : IComparable<Decomposition>, IEquatable<Decomposition>
	{
		public static Decomposition Empty = new(ImmutableArray<long>.Empty);

		// public override string ToString()
		// 	=> $"[ {EffectiveCost} = { string.Join(' ', Outputs) } ]";

		// Convenience constructor for tests
		public Decomposition(params int[] Outputs)
		{
			this.Outputs = Outputs.Select(x => (long)x).ToImmutableArray();
			EffectiveCost = this.Outputs.Sum();
		}

		public Decomposition(ImmutableArray<long> Outputs)
		{
			this.Outputs = Outputs;
			EffectiveCost = Outputs.Sum();
		}

		// Fake decomposition, only used for EffectiveCostComparer
		internal Decomposition(long EffectiveCost)
		{
			this.Outputs = ImmutableArray<long>.Empty;
			this.EffectiveCost = EffectiveCost;
		}

		// This could also be ImmutableArray<Money>, but using longs consumes
		// significantly less memory.
		public ImmutableArray<long> Outputs { get; private init; }

		public long EffectiveCost { get; private init; }

		public Decomposition Extend(long output) => this with {
			Outputs = Outputs.Add(output),
			EffectiveCost = this.EffectiveCost + output,
		};

		// Natural order is descending
		public int CompareTo(Decomposition other)
		{
			// Total effective value descending
			var x = other.EffectiveCost.CompareTo(this.EffectiveCost);
			if (x != 0) // FIXME is there a cleaner way to short circuit?
			{
				return x;
			}

			// Lexicographically descending
			x = this.Outputs.Length.CompareTo(other.Outputs.Length);
			if (x != 0)
			{
				return x;
			}

			// Note x & y are reversed in per element comparison
			return Enumerable.Zip(this.Outputs, other.Outputs, (x, y) => y.CompareTo(x)).FirstOrDefault(x => x != 0);
		}

		public bool Equals(Decomposition other)
			=> EffectiveCost == other.EffectiveCost && Outputs.SequenceEqual(other.Outputs);

		public override int GetHashCode() => Outputs.GetHashCode();
	}
}
