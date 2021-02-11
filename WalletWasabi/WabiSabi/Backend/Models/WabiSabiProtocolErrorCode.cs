using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Models
{
	public enum WabiSabiProtocolErrorCode
	{
		RoundNotFound,
		WrongPhase,
		InputSpent,
		InputUnconfirmed,
		InputImmature,
		InputScriptNotAllowed,
		WrongRoundSignature,
		TooManyInputs,
		NotEnoughFunds,
		TooMuchFunds,
		NonUniqueInputs
	}
}
