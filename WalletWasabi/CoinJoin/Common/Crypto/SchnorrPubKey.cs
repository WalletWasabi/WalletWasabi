using NBitcoin;
using Newtonsoft.Json;
using System;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.CoinJoin.Common.Crypto
{
	[JsonObject(MemberSerialization.OptIn)]
	public class SchnorrPubKey : IEquatable<SchnorrPubKey>
	{
		[JsonConstructor]
		public SchnorrPubKey(PubKey signerPubKey, PubKey rPubKey)
		{
			SignerPubKey = Guard.NotNull(nameof(signerPubKey), signerPubKey);
			RpubKey = Guard.NotNull(nameof(rPubKey), rPubKey);
		}

		public SchnorrPubKey(Key signerKey, Key rKey)
		{
			SignerPubKey = Guard.NotNull(nameof(signerKey), signerKey?.PubKey);
			RpubKey = Guard.NotNull(nameof(rKey), rKey?.PubKey);
		}

		[JsonProperty]
		[JsonConverter(typeof(PubKeyJsonConverter))]
		public PubKey SignerPubKey { get; }

		[JsonProperty]
		[JsonConverter(typeof(PubKeyJsonConverter))]
		public PubKey RpubKey { get; }

		#region EqualityAndComparison

		public override bool Equals(object? obj) => Equals(obj as SchnorrPubKey);

		public bool Equals(SchnorrPubKey? other) => this == other;

		public override int GetHashCode() => (SignerPubKey, RpubKey).GetHashCode();

		public static bool operator ==(SchnorrPubKey? x, SchnorrPubKey? y) => (x?.SignerPubKey, x?.RpubKey) == (y?.SignerPubKey, y?.RpubKey);

		public static bool operator !=(SchnorrPubKey? x, SchnorrPubKey? y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
