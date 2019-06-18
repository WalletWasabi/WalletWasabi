using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Models
{
	public static class ShieldLevelHelper
	{
		public enum TargetPrivacy
		{
			None,
			Some,
			Fine,
			Strong,
		}

		public static TargetPrivacy GetTargetPrivacy(int? mixUntilAnonymitySet)
		{
			if (mixUntilAnonymitySet == Global.Instance.Config.PrivacyLevelSome)
			{
				return TargetPrivacy.Some;
			}

			if (mixUntilAnonymitySet == Global.Instance.Config.PrivacyLevelFine)
			{
				return TargetPrivacy.Fine;
			}

			if (mixUntilAnonymitySet == Global.Instance.Config.PrivacyLevelStrong)
			{
				return TargetPrivacy.Strong;
			}
			//the levels changed in the config file, adjust
			if (mixUntilAnonymitySet < Global.Instance.Config.PrivacyLevelSome)
			{
				return TargetPrivacy.None; //choose the lower
			}

			if (mixUntilAnonymitySet < Global.Instance.Config.PrivacyLevelFine)
			{
				return TargetPrivacy.Some;
			}

			if (mixUntilAnonymitySet < Global.Instance.Config.PrivacyLevelStrong)
			{
				return TargetPrivacy.Fine;
			}

			if (mixUntilAnonymitySet > Global.Instance.Config.PrivacyLevelFine)
			{
				return TargetPrivacy.Strong;
			}

			return TargetPrivacy.None;
		}

		public static int GetTargetLevel(TargetPrivacy target)
		{
			switch (target)
			{
				case TargetPrivacy.None:
					return 0;

				case TargetPrivacy.Some:
					return Global.Instance.Config.PrivacyLevelSome.Value;

				case TargetPrivacy.Fine:
					return Global.Instance.Config.PrivacyLevelFine.Value;

				case TargetPrivacy.Strong:
					return Global.Instance.Config.PrivacyLevelStrong.Value;
			}
			return 0;
		}

		public static int AdjustTargetLevel(int mixUntilAnonymitySet)
		{
			var res = GetTargetPrivacy(mixUntilAnonymitySet); // select a valid level
			return GetTargetLevel(res);
		}
	}
}
