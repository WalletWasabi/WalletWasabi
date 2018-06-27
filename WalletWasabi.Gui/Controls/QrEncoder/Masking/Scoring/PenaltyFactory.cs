using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gma.QrCodeNet.Encoding.Masking.Scoring
{
	/// <summary>
	/// Description of PenaltyFactory.
	/// </summary>
	internal class PenaltyFactory
	{
		
		internal Penalty CreateByRule(PenaltyRules penaltyRule)
		{
			switch(penaltyRule)
			{
				case PenaltyRules.Rule01:
					return new Penalty1();
				case PenaltyRules.Rule02:
					return new Penalty2();
				case PenaltyRules.Rule03:
					return new Penalty3();
				case PenaltyRules.Rule04:
					return new Penalty4();
				default:
					throw new ArgumentException(string.Format("Unsupport penalty rule : {0}", penaltyRule), "penaltyRule");
					
			}
		}
		
		
		
		internal IEnumerable<Penalty> AllRules()
		{
			foreach(PenaltyRules penaltyRule in Enum.GetValues(typeof(PenaltyRules)))
			{
				yield return CreateByRule(penaltyRule);
			}
		}
	}
}
