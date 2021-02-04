using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Models
{
	public class InputRoundSignaturePair
	{
		public InputRoundSignaturePair(OutPoint input, byte[] roundSignature)
		{
			Input = input;
			RoundSignature = roundSignature;
		}

		public OutPoint Input { get; }

		public byte[] RoundSignature { get; }
	}
}
