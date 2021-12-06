using System.Threading;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.Interfaces;
using WalletWasabi.Interfaces.EventSourcing;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public class TestEventStore : EventStore, IDisposable
	{
		public TestEventStore(IEventRepository eventRepository, IAggregateFactory aggregateFactory, ICommandProcessorFactory commandProcessorFactory)
			: base(eventRepository, aggregateFactory, commandProcessorFactory)
		{
		}

		public SemaphoreSlim PreparedSemaphore { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim ConflictedSemaphore { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim AppendedSemaphore { get; } = new SemaphoreSlim(0);

		public Action? PreparedCallback { get; set; }
		public Action? ConflictedCallback { get; set; }
		public Action? AppendedCallback { get; set; }

		protected override void Prepared()
		{
			base.Prepared();
			PreparedSemaphore.Release();
			PreparedCallback?.Invoke();
		}

		protected override void Conflicted()
		{
			base.Conflicted();
			ConflictedSemaphore.Release();
			ConflictedCallback?.Invoke();
		}

		protected override void Appended()
		{
			base.Appended();
			AppendedSemaphore.Release();
			AppendedCallback?.Invoke();
		}

		public void Dispose()
		{
			PreparedSemaphore.Dispose();
			ConflictedSemaphore.Dispose();
			AppendedSemaphore.Dispose();
		}
	}
}
