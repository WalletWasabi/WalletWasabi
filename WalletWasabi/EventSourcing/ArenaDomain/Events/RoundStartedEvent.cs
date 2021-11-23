using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.ArenaDomain.Aggregates;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.EventSourcing.ArenaDomain.Events
{
	public record RoundStartedEvent(RoundParameters2 RoundParameters) : IEvent, IRoundClientEvent;
}
