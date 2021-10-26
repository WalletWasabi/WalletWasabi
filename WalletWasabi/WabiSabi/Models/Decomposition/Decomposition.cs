using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models.Decomposition
{
	// TODO use Money instead of long.
	// Unfortunately this can consume a significant amount of memory, so in
	// order to provide the nicer Money based API, DecompositionsOfASize should
	// first be modified to no longer use this type internally. For now, this
	// class uses a long of the effective cost and only that to represent self
	// spend outputs, which is less convenient but does reduce memory
	// consumption somewhat.
	public sealed record Decomposition : IComparable<Decomposition>, IEquatable<Decomposition>
	{
		// Useful for prettifying test assertions, or alternatively as a debug
		// display attribute:
		// public override string ToString()
		// 	=> $"[ {EffectiveCost} = { string.Join(' ', Outputs) } ]";

		// Construct a singleton
		public Decomposition(long EffectiveCost)
		{
			this.Outputs = ImmutableArray.Create<long>(EffectiveCost);
			this.EffectiveCost = EffectiveCost;
		}

		// Convenience constructor for tests
		internal Decomposition(params int[] Outputs)
		{
			this.Outputs = Outputs.OrderByDescending(x => x).Select(x => (long)x).ToImmutableArray();
			EffectiveCost = this.Outputs.Sum();
		}

		public ImmutableArray<long> Outputs { get; private init; }

		public long EffectiveCost { get; private init; }

		public Decomposition Extend(long output)
			=> (output <= Outputs.Last()) switch
			{
				true => this with
				{
					Outputs = Outputs.Add(output),
					EffectiveCost = EffectiveCost + output,
				},
				_ => throw new InvalidOperationException("Generated decompositions must be monotonically decreasing"),
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
