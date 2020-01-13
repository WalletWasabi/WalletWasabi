using System;
using System.Collections.Generic;

namespace Gma.QrCodeNet.Encoding.Masking.Scoring
{
	/// <summary>
	/// Description of PenaltyFactory.
	/// </summary>
	internal class PenaltyFactory
	{
		internal Penalty CreateByRule(PenaltyRules penaltyRule)
		{
			return penaltyRule switch
			{
				PenaltyRules.Rule01 => new Penalty1(),
				PenaltyRules.Rule02 => new Penalty2(),
				PenaltyRules.Rule03 => new Penalty3(),
				PenaltyRules.Rule04 => new Penalty4(),
				_ => throw new ArgumentException($"Unsupport penalty rule: {penaltyRule}", nameof(penaltyRule))
			};
		}

		internal IEnumerable<Penalty> AllRules()
		{
			foreach (PenaltyRules penaltyRule in Enum.GetValues(typeof(PenaltyRules)))
			{
				yield return CreateByRule(penaltyRule);
			}
		}
	}
}
