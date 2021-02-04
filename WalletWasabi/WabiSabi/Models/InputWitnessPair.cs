using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Models
{
	public class InputWitnessPair
	{
		public InputWitnessPair(OutPoint input, WitScript witness)
		{
			Input = input;
			Witness = witness;
		}

		public OutPoint Input { get; }
		public WitScript Witness { get; }
	}
}
