using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianCoinJoin.Models
{
    public class InputsRequest
    {
		public InputProofModel[] Inputs { get; set; }
		public string BlindedOutput { get; set; }
		public string ChangeOutput { get; set; }

		public bool IsSame(InputsRequest other)
		{
			if (BlindedOutput != other.BlindedOutput) return false;
			if (ChangeOutput != other.ChangeOutput) return false;
			int inputsCount = Inputs.Count();
			if (inputsCount != other.Inputs.Count()) return false;
			for (int i = 0; i < inputsCount; i++)
			{
				if (Inputs[i].Input != other.Inputs[i].Input) return false;
				if (Inputs[i].Proof != other.Inputs[i].Proof) return false;
			}

			return true;
		}
	}
}