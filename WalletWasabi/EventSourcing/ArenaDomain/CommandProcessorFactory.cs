using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing.ArenaDomain.CommandProcessor;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Exceptions;

namespace WalletWasabi.EventSourcing.ArenaDomain
{
	internal class CommandProcessorFactory : ICommandProcessorFactory
	{
		public ICommandProcessor Create(string aggregateType)
		{
			if (TryCreate(aggregateType, out var commandProcessor))
			{
				return commandProcessor
					?? throw new AssertionFailedException($"CommandProcessorFactory returned null for aggregateType: '{aggregateType}'");
			}
			else
			{
				throw new InvalidOperationException($"CommandProcessor is missing for aggregate type '{aggregateType}'.");
			}
		}

		public bool TryCreate(string aggregateType, out ICommandProcessor commandProcessor)
		{
			switch (aggregateType)
			{
				case nameof(RoundAggregate):
					commandProcessor = new RoundCommandProcessor();
					return true;

				default:
					commandProcessor = null!;
					return false;
			}
		}
	}
}
