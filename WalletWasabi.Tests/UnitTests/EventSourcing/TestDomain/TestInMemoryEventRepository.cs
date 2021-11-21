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

		public SemaphoreSlim LockedSemaphoreToRelease { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim AppendedSemaphoreToRelease { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim UnlockedSemaphoreToRelease { get; } = new SemaphoreSlim(0);

		public SemaphoreSlim LockedSemaphoreToWait { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim AppendedSemaphoreToWait { get; } = new SemaphoreSlim(0);
		public SemaphoreSlim UnlockedSemaphoreToWait { get; } = new SemaphoreSlim(0);

		public TestInMemoryEventRepository(ITestOutputHelper output)
		{
			Output = output;
		}

		protected override void Locked()
		{
			base.Locked();
			Output.WriteLine("Locked");
			LockedSemaphoreToRelease.Release();
			LockedSemaphoreToWait.Wait();
		}

		protected override void Appended()
		{
			base.Appended();
			Output.WriteLine("Appended");
			AppendedSemaphoreToRelease.Release();
			AppendedSemaphoreToWait.Wait();
		}

		protected override void Unlocked()
		{
			base.Unlocked();
			Output.WriteLine("Unlocked");
			UnlockedSemaphoreToRelease.Release();
			UnlockedSemaphoreToWait.Wait();
		}
	}
}
