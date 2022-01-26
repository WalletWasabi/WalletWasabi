using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Crypto;

[JsonObject(MemberSerialization.OptIn)]
public class SchnorrKey : IEquatable<SchnorrKey>
{
	[JsonConstructor]
	public SchnorrKey(Key signerKey, Key rKey)
	{
		SignerKey = Guard.NotNull(nameof(signerKey), signerKey);
		Rkey = Guard.NotNull(nameof(rKey), rKey);
	}

	[JsonProperty]
	[JsonConverter(typeof(KeyJsonConverter))]
	public Key SignerKey { get; }

	[JsonProperty]
	[JsonConverter(typeof(KeyJsonConverter))]
	public Key Rkey { get; }

	#region EqualityAndComparison

	public override bool Equals(object? obj) => Equals(obj as SchnorrKey);

	public bool Equals(SchnorrKey? other) => this == other;

	public override int GetHashCode() => (SignerKey, Rkey).GetHashCode();

	public static bool operator ==(SchnorrKey? x, SchnorrKey? y) => (x?.SignerKey, x?.Rkey) == (y?.SignerKey, y?.Rkey);

	public static bool operator !=(SchnorrKey? x, SchnorrKey? y) => !(x == y);

	#endregion EqualityAndComparison
}
