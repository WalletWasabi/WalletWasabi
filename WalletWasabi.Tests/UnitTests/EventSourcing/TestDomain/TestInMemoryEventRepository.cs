using System.Threading;
using WalletWasabi.EventSourcing;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public class TestInMemoryEventRepository : InMemoryEventRepository, IDisposable
	{
		public TestInMemoryEventRepository(ITestOutputHelper output)
		{
			Output = output;
		}

		protected ITestOutputHelper Output { get; init; }

		public SemaphoreSlim ValidatedSemaphore { get; } = new(0);
		public SemaphoreSlim ConflictedSemaphore { get; } = new(0);
		public SemaphoreSlim AppendedSemaphore { get; } = new(0);

		public Action? ValidatedCallback { get; set; }
		public Action? ConflictedCallback { get; set; }
		public Action? AppendedCallback { get; set; }

		protected override void Validated()
		{
			base.Validated();
			Output.WriteLine(nameof(Validated));
			ValidatedSemaphore.Release();
			ValidatedCallback?.Invoke();
		}

		protected override void Conflicted()
		{
			base.Conflicted();
			Output.WriteLine(nameof(Conflicted));
			ConflictedSemaphore.Release();
			ConflictedCallback?.Invoke();
		}

		protected override void Appended()
		{
			base.Appended();
			Output.WriteLine(nameof(Appended));
			AppendedSemaphore.Release();
			AppendedCallback?.Invoke();
		}

		public void Dispose()
		{
			ValidatedSemaphore.Dispose();
			ConflictedSemaphore.Dispose();
			AppendedSemaphore.Dispose();
		}
	}
}
