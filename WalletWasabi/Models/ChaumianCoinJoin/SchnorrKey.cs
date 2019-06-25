using NBitcoin;
using Newtonsoft.Json;
using System;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
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

		public SchnorrPubKey SchnorrPubKey => new SchnorrPubKey(SignerKey, Rkey);

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is SchnorrKey && this == (SchnorrKey)obj;

		public bool Equals(SchnorrKey other) => this == other;

		public override int GetHashCode() => SignerKey.GetHashCode() ^ Rkey.GetHashCode();

		public static bool operator ==(SchnorrKey x, SchnorrKey y) => y?.SignerKey == x?.SignerKey && y?.Rkey == x?.Rkey;

		public static bool operator !=(SchnorrKey x, SchnorrKey y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
