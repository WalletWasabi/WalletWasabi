﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler.Models
{
	public enum TumblerPhase
	{
		InputRegistration,
		InputConfirmation,
		OutputRegistration,
		Signing
	}
}
