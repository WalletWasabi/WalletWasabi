using NBitcoin.Secp256k1;
using WalletWasabi.Helpers;

namespace WalletWasabi.Crypto.Groups;

public class GroupElement : IEquatable<GroupElement>
{
	public GroupElement(GE groupElement)
	{
		if (groupElement.IsInfinity)
		{
			LazyGe = new Lazy<GE>(() => GE.Infinity);
		}
		else
		{
			Guard.True($"{nameof(groupElement)}.{nameof(groupElement.IsValidVariable)}", groupElement.IsValidVariable);
			LazyGe = new Lazy<GE>(() => new GE(groupElement.x.Normalize(), groupElement.y.Normalize()));
		}

		Gej = Ge.ToGroupElementJacobian(); // eagerly initialize Ge property
	}

	// Since GEJ.IsValidVariable, this constructor is private
	internal GroupElement(GEJ groupElementJacobian)
	{
		if (groupElementJacobian.IsInfinity)
		{
			LazyGe = new Lazy<GE>(() => GE.Infinity);
			Gej = Ge.ToGroupElementJacobian(); // eagerly initialize Ge property
		}
		else
		{
			GE ComputeAffineCoordinates()
			{
				var groupElement = groupElementJacobian.ToGroupElement();
				return new GE(groupElement.x.Normalize(), groupElement.y.Normalize());
			}
			LazyGe = new Lazy<GE>(ComputeAffineCoordinates); // avoid computing affine coordinates until needed
			Gej = groupElementJacobian;
		}
	}

	public static GroupElement Infinity { get; } = new GroupElement(GE.Infinity);

	private GEJ Gej { get; }
	private Lazy<GE> LazyGe { get; }
	internal GE Ge => LazyGe.Value;
	private bool IsGeCreated => LazyGe.IsValueCreated;

	public bool IsInfinity => Gej.IsInfinity;

	public override bool Equals(object? obj) => Equals(obj as GroupElement);

	public bool Equals(GroupElement? other) => this == other;

	public override int GetHashCode() => Ge.GetHashCode();

	public static bool operator ==(GroupElement? a, GroupElement? b)
	{
		if (a is null && b is null)
		{
			return true;
		}
		else if (a is null || b is null)
		{
			return false;
		}
		else if (a.IsInfinity && b.IsInfinity)
		{
			return true;
		}
		else if (a.IsGeCreated || b.IsGeCreated)
		{
			return a.IsInfinity == b.IsInfinity && a.Ge.x == b.Ge.x && a.Ge.y == b.Ge.y;
		}
		else
		{
			return (a - b).IsInfinity;
		}
	}

	public static bool operator !=(GroupElement? a, GroupElement? b) => !(a == b);

	/// <summary>
	/// ToString is only used for nice visual representation during debugging. Do not rely on the result for anything else.
	/// </summary>
	public override string ToString()
	{
		if (IsInfinity)
		{
			return "Infinity";
		}
		else if (Generators.TryGetFriendlyGeneratorName(this, out var generatorName))
		{
			return $"{generatorName}, {Ge.x.ToC("x")}{Ge.y.ToC("y")}";
		}
		else
		{
			return $"{Ge.x.ToC("x")}{Ge.y.ToC("y")}";
		}
	}

	// GEJ.AddVariable(GE) is more efficient than GEJ.AddVariable(GEJ).
	public static GroupElement operator +(GroupElement a, GroupElement b)
	{
		if (b.IsGeCreated)
		{
			return new GroupElement(a.Gej.AddVariable(b.Ge, out _));
		}
		else if (a.IsGeCreated)
		{
			return new GroupElement(b.Gej.AddVariable(a.Ge, out _));
		}
		else
		{
			return new GroupElement(a.Gej.AddVariable(b.Gej, out _));
		}
	}

	public static GroupElement operator -(GroupElement a, GroupElement b)
		=> a + b.Negate();

	/// <param name="scalar">It's ok for the scalar to overflow.</param>
	public static GroupElement operator *(Scalar scalar, GroupElement groupElement)
	{
		// For some strange reason scalar * GE.Infinity isn't infinity. Let's fix it as it should be, since:
		// 2 * GE.Infinity = GE.Infinity + GE.Infinity = GE.Infinity.
		if (groupElement.IsInfinity)
		{
			return Infinity;
		}

		return new GroupElement(scalar * groupElement.Ge);
	}

	/// <param name="scalar">It's ok for the scalar to overflow.</param>
	public static GroupElement operator *(GroupElement groupElement, Scalar scalar) => scalar * groupElement;

	public GroupElement Negate() => IsGeCreated ? new GroupElement(Ge.Negate()) : new GroupElement(Gej.Negate());

	public byte[] ToBytes()
	{
		// Buffer to store the serialized Group Element in its **compressed** format.
		// It requires 32 bytes for the 256bits `x` coordinate and an extra byte for
		// the EVEN/ODD flag.
		const int CompressedLength = 32 + 1;
		Span<byte> buffer = new byte[CompressedLength];

		var x = Ge.x;
		var y = Ge.y;

		buffer[0] = (Ge.IsInfinity, y.IsOdd) switch
		{
			(true, _) => 0, // see http://www.secg.org/sec1-v2.pdf sections 2.3.3-4:
			(false, true) => GE.SECP256K1_TAG_PUBKEY_ODD,
			(false, false) => GE.SECP256K1_TAG_PUBKEY_EVEN,
		};
		x.WriteToSpan(buffer[1..]);
		return buffer.ToArray();
	}

	public static GroupElement FromBytes(byte[] bytes)
	{
		const int CompressedLength = 32 + 1;
		Guard.Same($"{nameof(bytes)}.{nameof(bytes.Length)}", CompressedLength, bytes.Length);

		static GroupElement Parse(Span<byte> buffer, bool isOdd) =>
			FE.TryCreate(buffer, out var x) && GE.TryCreateXOVariable(x, isOdd, out var ge)
			? new GroupElement(ge)
			: throw new ArgumentException("Argument is not a valid group element.", nameof(bytes));

		return bytes[0] switch
		{
			0 => Infinity,
			GE.SECP256K1_TAG_PUBKEY_ODD => Parse(bytes.AsSpan()[1..], isOdd: true),
			GE.SECP256K1_TAG_PUBKEY_EVEN => Parse(bytes.AsSpan()[1..], isOdd: false),
			_ => throw new ArgumentException($"Argument is not a well-formatted group element.", nameof(bytes))
		};
	}
}
