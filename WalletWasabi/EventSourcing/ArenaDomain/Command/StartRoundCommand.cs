using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.EventSourcing.ArenaDomain.Command
{
	public record StartRoundCommand(RoundParameters RoundParameters, Guid IdempotenceId) : ICommand;
}
