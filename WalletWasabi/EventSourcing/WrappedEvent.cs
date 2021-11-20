using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing
{
	public record WrappedEvent(long SequenceId);
}
