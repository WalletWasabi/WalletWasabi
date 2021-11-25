using System;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Exceptions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public class TestDomainAggregateFactory : IAggregateFactory
	{
		public IAggregate Create(string aggregateType)
		{
			if (TryCreate(aggregateType, out var aggregate))
			{
				return aggregate
					?? throw new AssertionFailedException($"AggregateFactory returned null for aggregateType: '{aggregateType}'");
			}
			else
			{
				throw new InvalidOperationException($"AggregateFactory is missing for aggregate type '{aggregateType}'.");
			}
		}

		public bool TryCreate(string aggregateType, out IAggregate aggregate)
		{
			switch (aggregateType)
			{
				case nameof(TestRoundAggregate):
					aggregate = new TestRoundAggregate();
					return true;

				default:
					aggregate = null!;
					return false;
			}
		}
	}
}
