using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using WalletWasabi.CoinJoin.Common.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.CoinJoin.Coordinator.MixingLevels
{
	[JsonObject(MemberSerialization.OptIn)]
	public class MixingLevel : IEquatable<MixingLevel>
	{
		public MixingLevel(Money denomination, Signer signer)
		{
			Denomination = Guard.NotNull(nameof(denomination), denomination);
			Signer = Guard.NotNull(nameof(signer), signer);
			var signerKey = Guard.NotNull(nameof(signer.Key), signer.Key);
			var rKey = Guard.NotNull(nameof(signer.R), signer.R);

			SchnorrKey = new SchnorrKey(signerKey, rKey);
		}

		public MixingLevel(Money denomination, Key signerKey, Key rKey)
		{
			Denomination = Guard.NotNull(nameof(denomination), denomination);
			signerKey = Guard.NotNull(nameof(signerKey), signerKey);
			rKey = Guard.NotNull(nameof(rKey), rKey);
			SchnorrKey = new SchnorrKey(signerKey, rKey);

			Signer = SchnorrKey.CreateSigner();
		}

		[JsonConstructor]
		public MixingLevel(Money denomination, SchnorrKey schnorrKey)
		{
			Denomination = Guard.NotNull(nameof(denomination), denomination);
			SchnorrKey = Guard.NotNull(nameof(schnorrKey), schnorrKey);

			Signer = SchnorrKey.CreateSigner();
		}

		public Signer Signer { get; }

		[JsonProperty]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Denomination { get; }

		[JsonProperty]
		public SchnorrKey SchnorrKey { get; }

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is MixingLevel level && this == level;

		public bool Equals(MixingLevel other) => this == other;

		public override int GetHashCode() => Denomination.GetHashCode() ^ SchnorrKey.GetHashCode();

		public static bool operator ==(MixingLevel x, MixingLevel y) => y?.Denomination == x?.Denomination && y?.SchnorrKey == x?.SchnorrKey;

		public static bool operator !=(MixingLevel x, MixingLevel y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
