using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;
using static NBitcoin.Crypto.SchnorrBlinding;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	[JsonObject(MemberSerialization.OptIn)]
	public class MixingLevels
	{
		[JsonConstructor]
		public MixingLevels(IEnumerable<MixingLevel> levels)
		{
			Create(levels);
		}

		public MixingLevels(Money baseDenomination, Signer signer)
		{
			baseDenomination = Guard.MinimumAndNotNull(nameof(baseDenomination), baseDenomination, Money.Zero);
			signer = Guard.NotNull(nameof(signer), signer);
			Guard.NotNull(nameof(signer.Key), signer.Key);
			Guard.NotNull(nameof(signer.R), signer.R);

			Create(new List<MixingLevel> { new MixingLevel(baseDenomination, signer) });
		}

		private void Create(IEnumerable<MixingLevel> levels)
		{
			Levels = Guard.NotNullOrEmpty(nameof(levels), levels).ToList();
		}

		[JsonProperty]
		public List<MixingLevel> Levels { get; private set; }

		public void AddNewLevel()
		{
			var signer = new Signer(new Key());
			Money denomination = Levels.Last().Denomination * 2;
			Levels.Add(new MixingLevel(denomination, signer));
		}

		public Money GetBaseDenomination() => Levels.First().Denomination;

		public bool TryGetDenomination(int level, out Money denomination)
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

		private IEnumerable<SchnorrPubKey> _schnorrPubKeys;

		public IEnumerable<SchnorrPubKey> SchnorrPubKeys
		{
			get
			{
				if (_schnorrPubKeys?.Count() != Levels?.Count()) // Signing keys don't change, but more levels may be added. (Although even that's unlikely.)
				{
					_schnorrPubKeys = Levels.Select(x => x.Signer.GetSchnorrPubKey());
				}
				return _schnorrPubKeys;
			}
			set { _schnorrPubKeys = value; }
		}

		public int GetMaxLevel() => Levels.Count() - 1;
	}
}
