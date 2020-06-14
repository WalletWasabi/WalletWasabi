using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using NBitcoin;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	public class ServiceConfiguration
	{
		public ServiceConfiguration(
			string mixUntilAnonymitySet,
			int privacyLevelSome,
			int privacyLevelFine,
			int privacyLevelStrong,
			EndPoint bitcoinCoreEndPoint,
			Money dustThreshold)
		{
			MixUntilAnonymitySet = Guard.NotNull(nameof(mixUntilAnonymitySet), mixUntilAnonymitySet);
			PrivacyLevelSome = Guard.NotNull(nameof(privacyLevelSome), privacyLevelSome);
			PrivacyLevelFine = Guard.NotNull(nameof(privacyLevelFine), privacyLevelFine);
			PrivacyLevelStrong = Guard.NotNull(nameof(privacyLevelStrong), privacyLevelStrong);
			BitcoinCoreEndPoint = Guard.NotNull(nameof(bitcoinCoreEndPoint), bitcoinCoreEndPoint);
			DustThreshold = Guard.NotNull(nameof(dustThreshold), dustThreshold);
		}

		public string MixUntilAnonymitySet { get; set; }
		public int PrivacyLevelSome { get; set; }
		public int PrivacyLevelFine { get; set; }
		public int PrivacyLevelStrong { get; set; }
		public EndPoint BitcoinCoreEndPoint { get; set; }
		public Money DustThreshold { get; set; }

		public int GetMixUntilAnonymitySetValue()
		{
			if (MixUntilAnonymitySet == Models.MixUntilAnonymitySet.PrivacyLevelSome.ToString())
			{
				return PrivacyLevelSome;
			}
			else if (MixUntilAnonymitySet == Models.MixUntilAnonymitySet.PrivacyLevelFine.ToString())
			{
				return PrivacyLevelFine;
			}
			else
			{
				return PrivacyLevelStrong;
			}
		}
	}
}
