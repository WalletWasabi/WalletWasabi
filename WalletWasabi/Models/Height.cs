namespace WalletWasabi.Models;

public abstract record Height : IComparable<Height>
{
	public static readonly Height Mempool = new MempoolHeight();
	public static readonly Height Unknown = new UnknownHeight();

	private record MempoolHeight : Height;

	private record UnknownHeight : Height;

	public record ChainHeight(uint Height) : Height
	{
		public static readonly ChainHeight Genesis = new(0u);

		public static implicit operator ChainHeight(uint value) => new(value);
		public static implicit operator uint(ChainHeight height) => height.Height;

		public static ChainHeight operator ++(ChainHeight me) => me + 1;
		public static ChainHeight operator --(ChainHeight me) => me - 1;
		public static ChainHeight operator +(ChainHeight height, int value) => new(height.Height + (uint)value);
		public static ChainHeight operator -(ChainHeight height, int value) => height.Height - value >= 0
			? new(height.Height - (uint)value)
			: throw new ArgumentException($"{nameof(ChainHeight)} height can not be negative");

		public static ChainHeight Max(ChainHeight h1, ChainHeight h2) => h1 > h2 ? h1 : h2;
		public static ChainHeight Mim(ChainHeight h1, ChainHeight h2) => h1 < h2 ? h1 : h2;
	}

	public static bool operator >(Height x, Height y) => x.CompareTo(y) > 0;
	public static bool operator <(Height x, Height y) => x.CompareTo(y) < 0;
	public static bool operator >=(Height x, Height y) => x.CompareTo(y) >= 0;
	public static bool operator <=(Height x, Height y) => x.CompareTo(y) <= 0;

	public int CompareTo(Height? other) =>
		(this, other) switch
		{
			(UnknownHeight, UnknownHeight) => 0,
			(UnknownHeight, _) => 1,
			(_, UnknownHeight) => -1,
			(MempoolHeight, MempoolHeight) => 0,
			(MempoolHeight, _) => 1,
			(_, MempoolHeight) => -1,
			(ChainHeight a, ChainHeight b) => a.Height.CompareTo(b.Height),
			_ => throw new ArgumentOutOfRangeException()
		};

	public static Height Max(Height h1, Height h2) => h1 > h2 ? h1 : h2;
	public static Height Mim(Height h1, Height h2) => h1 < h2 ? h1 : h2;

	public static bool TryParse(string heightOrHeightType, out Height? height)
	{
		if (string.IsNullOrWhiteSpace(heightOrHeightType))
		{
			height = null;
			return false;
		}

		if (heightOrHeightType == "Mempool")
		{
			height = Mempool;
			return true;
		}

		if (heightOrHeightType == "Unknown")
		{
			height = Unknown;
			return true;
		}

		if (uint.TryParse(heightOrHeightType, out var h))
		{
			height = new ChainHeight(h);
			return true;
		}

		height = null;
		return false;
	}

	public sealed override string? ToString() =>
		this switch
		{
			MempoolHeight => "Mempool",
			UnknownHeight => "Unknown",
			ChainHeight(var h) => h.ToString(),
			_ => throw new ArgumentOutOfRangeException()
		};
}

