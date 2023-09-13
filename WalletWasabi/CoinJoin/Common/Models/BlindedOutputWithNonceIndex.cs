using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Models;

public class BlindedOutputWithNonceIndex : IEquatable<BlindedOutputWithNonceIndex>
{
	public BlindedOutputWithNonceIndex(int n, uint256 blindedOutput)
	{
		N = n;
		BlindedOutput = blindedOutput;
	}

	public int N { get; set; }

	[JsonConverter(typeof(Uint256JsonConverter))]
	public uint256 BlindedOutput { get; set; }

	public override bool Equals(object? obj) => Equals(obj as BlindedOutputWithNonceIndex);

	public bool Equals(BlindedOutputWithNonceIndex? other) => this == other;

	public override int GetHashCode() => (BlindedOutput, N).GetHashCode();

	public static bool operator ==(BlindedOutputWithNonceIndex? x, BlindedOutputWithNonceIndex? y) => (x?.BlindedOutput, x?.N) == (y?.BlindedOutput, y?.N);

	public static bool operator !=(BlindedOutputWithNonceIndex? x, BlindedOutputWithNonceIndex? y) => !(x == y);
}
