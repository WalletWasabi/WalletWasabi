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
		public InputWitnessPair(uint inputIndex, WitScript witness)
		{
			InputIndex = inputIndex;
			Witness = witness;
		}

		public uint InputIndex { get; }
		public WitScript Witness { get; }
	}
}
