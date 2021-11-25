using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Interfaces.EventSourcing;
using WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.EventSourcing
{
	public class TestRoundTests
	{
		protected IEventRepository EventRepository { get; init; }
		protected IEventStore EventStore { get; init; }

		public TestRoundTests()
		{
			EventRepository = new InMemoryEventRepository();
			EventStore = new EventStore(
				EventRepository,
				new TestDomainAggregateFactory(),
				new TestDomainCommandProcessorFactory());
		}

		[Fact]
		public async Task StartRound_Success_Async()
		{
			// Arrange
			var command = new StartRound(1000, Guid.NewGuid());

			// Act
			var result = await EventStore.ProcessCommandAsync(command, nameof(TestRoundAggregate), "1");

			// Assert
			Assert.NotEmpty(result.NewEvents);
			Assert.True(result.LastSequenceId > 0);

			Assert.NotEmpty(await EventRepository.ListEventsAsync(nameof(TestRoundAggregate), "1"));
			Assert.True(
				(await EventRepository.ListAggregateIdsAsync(nameof(TestRoundAggregate)))
					.SequenceEqual(new[] { "1" }));
		}
	}
}
