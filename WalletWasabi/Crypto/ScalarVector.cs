using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto;

public class ScalarVector : IEnumerable<Scalar>, IEquatable<ScalarVector>
{
	[JsonConstructor]
	internal ScalarVector(IEnumerable<Scalar> scalars)
	{
		Guard.NotNullOrEmpty(nameof(scalars), scalars);
		Scalars = scalars.ToArray();
	}

	internal ScalarVector(params Scalar[] scalars)
		: this(scalars as IEnumerable<Scalar>)
	{
	}

	private IEnumerable<Scalar> Scalars { get; }

	public IEnumerator<Scalar> GetEnumerator() =>
		Scalars.GetEnumerator();

	public int Count => Scalars.Count();

	public static GroupElement operator *(ScalarVector scalars, GroupElementVector groupElements)
	{
		Guard.True(nameof(groupElements.Count), groupElements.Count == scalars.Count);

		var gej = ECMultContext.Instance.MultBatch(scalars.ToArray(), groupElements.Select(x => x.Ge).ToArray());
		return new GroupElement(gej);
	}

	public static ScalarVector operator *(Scalar scalar, ScalarVector scalars)
	{
		Guard.NotNull(nameof(scalars), scalars);

		return new ScalarVector(scalars.Select(si => scalar * si));
	}

	public static ScalarVector operator +(ScalarVector scalars1, ScalarVector scalars2)
	{
		Guard.NotNull(nameof(scalars1), scalars1);
		Guard.NotNull(nameof(scalars2), scalars2);
		Guard.True(nameof(scalars1.Count), scalars1.Count == scalars2.Count);

		return new ScalarVector(Enumerable.Zip(scalars1, scalars2, (s1, s2) => s1 + s2));
	}

	IEnumerator IEnumerable.GetEnumerator() =>
		GetEnumerator();

	public static bool operator ==(ScalarVector? x, ScalarVector? y) => x?.Equals(y) ?? false;

	public static bool operator !=(ScalarVector? x, ScalarVector? y) => !(x == y);

	public override int GetHashCode()
	{
		int hc = 0;

		foreach (var element in Scalars)
		{
			hc ^= element.GetHashCode();
			hc = (hc << 7) | (hc >> (32 - 7));
		}

		return hc;
	}

	public override bool Equals(object? other) => Equals(other as ScalarVector);

	// TODO: Define GetHashCode.
	public bool Equals(ScalarVector? other)
	{
		if (other is null)
		{
			return false;
		}

		return Scalars.SequenceEqual(other.Scalars);
	}
}
