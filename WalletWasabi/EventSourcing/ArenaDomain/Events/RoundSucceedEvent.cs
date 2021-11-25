using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.ArenaDomain.Events
{
	public record RoundSucceedEvent() : IEvent, IRoundClientEvent;
}
