using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public class TestInMemoryEventRepository : InMemoryEventRepository
	{
		protected ITestOutputHelper Output { get; init; }

		public SemaphoreSlim ValidatedSemaphore { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim ConflictedSemaphore { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim LockedSemaphore { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim AppendedSemaphore { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim UnlockedSemaphore { get; } = new SemaphoreSlim(0);

		public Action? ValidatedCallback { get; set; }
		public Action? ConflictedCallback { get; set; }
		public Action? LockedCallback { get; set; }
		public Action? AppendedCallback { get; set; }
		public Action? UnlockedCallback { get; set; }

		public TestInMemoryEventRepository(ITestOutputHelper output)
		{
			Output = output;
		}

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

		protected override void Locked()
		{
			base.Locked();
			Output.WriteLine(nameof(Locked));
			LockedSemaphore.Release();
			LockedCallback?.Invoke();
		}

		protected override void Appended()
		{
			base.Appended();
			Output.WriteLine(nameof(Appended));
			AppendedSemaphore.Release();
			AppendedCallback?.Invoke();
		}

		protected override void Unlocked()
		{
			base.Unlocked();
			Output.WriteLine(nameof(Unlocked));
			UnlockedSemaphore.Release();
			UnlockedCallback?.Invoke();
		}
	}
}
