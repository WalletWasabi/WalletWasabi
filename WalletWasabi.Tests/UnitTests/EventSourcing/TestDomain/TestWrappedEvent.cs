using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public record TestWrappedEvent(long SequenceId, string Value) : WrappedEvent(SequenceId);
}
