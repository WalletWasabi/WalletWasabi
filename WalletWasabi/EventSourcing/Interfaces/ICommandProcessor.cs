using System.Collections.Generic;

namespace WalletWasabi.EventSourcing.Interfaces
{
	public interface ICommandProcessor
	{
		public Result Process(ICommand command, IState aggregate);
	}
}
