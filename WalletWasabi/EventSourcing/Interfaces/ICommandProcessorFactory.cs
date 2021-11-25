using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface ICommandProcessorFactory
	{
		ICommandProcessor Create(string aggregateType);

		bool TryCreate(string aggregateType, out ICommandProcessor commandProcessor);
	}
}
