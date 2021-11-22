using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public record TestWrappedEvent(long SequenceId, string Value = "", IEvent? DomainEvent = null, Guid SourceId = default) : WrappedEvent(SequenceId, DomainEvent!, SourceId);
}
