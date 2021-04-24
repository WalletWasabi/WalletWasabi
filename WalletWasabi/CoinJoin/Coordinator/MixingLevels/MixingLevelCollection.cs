using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Helpers;
using static WalletWasabi.Crypto.SchnorrBlinding;

namespace WalletWasabi.CoinJoin.Coordinator.MixingLevels
{
	[JsonObject(MemberSerialization.OptIn)]
	public class MixingLevelCollection
	{
		private IEnumerable<PubKey> _signerPubKeys;

		[JsonConstructor]
		public MixingLevelCollection(IEnumerable<MixingLevel> levels)
		{
			Create(levels);
		}

		public MixingLevelCollection(Money baseDenomination, Signer signer)
		{
			baseDenomination = Guard.MinimumAndNotNull(nameof(baseDenomination), baseDenomination, Money.Zero);
			signer = Guard.NotNull(nameof(signer), signer);
			Guard.NotNull(nameof(signer.Key), signer.Key);

			Create(new List<MixingLevel> { new MixingLevel(baseDenomination, signer) });
		}

		[JsonProperty]
		public List<MixingLevel> Levels { get; private set; }

		public IEnumerable<PubKey> SignerPubKeys
		{
			get
			{
				if (_signerPubKeys?.Count() != Levels?.Count) // Signing keys do not change, but more levels may be added. (Although even that's unlikely.)
				{
					_signerPubKeys = Levels.Select(x => x.Signer.Key.PubKey);
				}
				return _signerPubKeys;
			}
			set => _signerPubKeys = value;
		}

		private void Create(IEnumerable<MixingLevel> levels)
		{
			Levels = Guard.NotNullOrEmpty(nameof(levels), levels).ToList();
		}

		public void AddNewLevel()
		{
			var signer = new Signer(new Key());
			Money denomination = Levels.Last().Denomination * 2;
			Levels.Add(new MixingLevel(denomination, signer));
		}

		public Money GetBaseDenomination() => Levels.First().Denomination;

		public bool TryGetDenomination(int level, [NotNullWhen(true)] out Money? denomination)
		{
			denomination = Money.Zero;
			try
			{
				denomination = GetLevel(level).Denomination;
			}
			catch (ArgumentOutOfRangeException)
			{
				return false;
			}

			return true;
		}

		public int Count() => Levels.Count;

		public MixingLevel GetBaseLevel() => Levels.First();

		public MixingLevel GetLevel(int i) => Levels.ElementAt(i);

		public IEnumerable<MixingLevel> GetAllLevels() => Levels.ToList();

		public IEnumerable<MixingLevel> GetLevelsExceptBase() => Levels.Skip(1).ToList();

		public int GetMaxLevel() => Levels.Count - 1;
	}
}
