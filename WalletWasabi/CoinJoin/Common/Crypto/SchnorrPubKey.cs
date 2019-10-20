using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using static NBitcoin.Crypto.SchnorrBlinding;

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

		public SchnorrPubKey(Signer signer)
		{
			signer = Guard.NotNull(nameof(signer), signer);
			var signerKey = signer?.Key;
			var rKey = signer?.R;
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

		public override bool Equals(object obj) => obj is SchnorrPubKey schnorrPubKey && this == schnorrPubKey;

		public bool Equals(SchnorrPubKey other) => this == other;

		public override int GetHashCode() => SignerPubKey.GetHashCode() ^ RpubKey.GetHashCode();

		public static bool operator ==(SchnorrPubKey x, SchnorrPubKey y) => y?.SignerPubKey == x?.SignerPubKey && y?.RpubKey == x?.RpubKey;

		public static bool operator !=(SchnorrPubKey x, SchnorrPubKey y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
