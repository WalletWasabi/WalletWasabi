using System;
using WalletWasabi.Models.ChaumianCoinJoin;

namespace WalletWasabi.Helpers
{
    public class CcjRoundHelper
    {
		public static bool ActionDisallowed(CcjRoundAction action, CcjRoundPhase phase, CcjRoundStatus status)
		{
			bool disallowed = false;

			switch (action)
			{
				case CcjRoundAction.AddingAlice:
				case CcjRoundAction.RemovingAlice:
				case CcjRoundAction.UpdatingAnonyminity:
					{
						disallowed = (phase != CcjRoundPhase.InputRegistration && phase != CcjRoundPhase.ConnectionConfirmation) || status != CcjRoundStatus.Running;
						return disallowed;
					}
				case CcjRoundAction.AddingBob:
					{
						disallowed = phase != CcjRoundPhase.OutputRegistration || status != CcjRoundStatus.Running;
						return disallowed;
					}
				default:
					return disallowed;
			}
		}
    }
}
