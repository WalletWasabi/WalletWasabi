using System.Collections.Generic;
using WalletWasabi.EventSourcing.Records;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface ICommandProcessor
	{
		public Result Process(ICommand command, IState aggregateState);
	}
}
