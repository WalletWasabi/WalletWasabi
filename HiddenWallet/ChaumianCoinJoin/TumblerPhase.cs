using System;
using System.Collections.Generic;
using System.Text;

namespace HiddenWallet.ChaumianCoinJoin
{
    public enum TumblerPhase
    {
		InputRegistration,
		ConnectionConfirmation,
		OutputRegistration,
		Signing,
	}

	public static class TumblerPhaseHelpers
	{
		public static TumblerPhase GetTumblerPhase(string phase)
		{
			if (phase == null) throw new ArgumentNullException(nameof(phase));

			foreach (TumblerPhase p in Enum.GetValues(typeof(TumblerPhase)))
			{
				if (phase.Equals(p.ToString(), StringComparison.OrdinalIgnoreCase))
				{
					return p;
				}
			}

			throw new NotSupportedException($"Phase does not exist: {phase}");
		}
	}
}
