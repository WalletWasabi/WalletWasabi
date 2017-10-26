using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Models
{
	public static class TumblerPhase
	{
		public const string InputRegistration = "InputRegistration";
		public const string InputConfirmation = "InputConfirmation";
		public const string OutputRegistration = "OutputRegistration";
		public const string Signing = "Signing";
	}
}
