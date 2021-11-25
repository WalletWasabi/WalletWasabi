using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.ArenaDomain.Events
{
	public record SigningStartedEvent() : IEvent, IRoundClientEvent;
}
