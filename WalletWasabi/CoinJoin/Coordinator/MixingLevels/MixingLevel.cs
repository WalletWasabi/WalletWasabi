using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters;
using static WalletWasabi.Crypto.SchnorrBlinding;

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

			SignerKey = signer.Key;
		}

		[JsonConstructor]
		public MixingLevel(Money denomination, Key signerKey)
		{
			Denomination = Guard.NotNull(nameof(denomination), denomination);
			SignerKey = Guard.NotNull(nameof(signerKey), signerKey);
			Signer = new Signer(signerKey);
		}

		public Signer Signer { get; }

		[JsonProperty]
		[JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Denomination { get; }

		[JsonProperty]
		public Key SignerKey { get; }

		#region EqualityAndComparison

		public override bool Equals(object obj) => Equals(obj as MixingLevel);

		public bool Equals(MixingLevel other) => this == other;

		public override int GetHashCode() => (Denomination, SignerKey).GetHashCode();

		public static bool operator ==(MixingLevel x, MixingLevel y) => y?.Denomination == x?.Denomination && y?.SignerKey == x?.SignerKey;

		public static bool operator !=(MixingLevel x, MixingLevel y) => !(x == y);

		#endregion EqualityAndComparison
	}
}
