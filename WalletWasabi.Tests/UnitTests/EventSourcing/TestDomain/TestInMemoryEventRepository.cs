using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.EventSourcing;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain;

public class TestInMemoryEventRepository : InMemoryEventRepository, IDisposable
{
	public TestInMemoryEventRepository(ITestOutputHelper testOutput)
	{
		TestOutput = testOutput;
	}

	protected ITestOutputHelper TestOutput { get; }

	public SemaphoreSlim ValidatedSemaphore { get; } = new(0);
	public SemaphoreSlim ConflictedSemaphore { get; } = new(0);
	public SemaphoreSlim AppendedSemaphore { get; } = new(0);

	public Func<Task>? ValidatedCallbackAsync { get; set; }
	public Func<Task>? ConflictedCallbackAsync { get; set; }
	public Func<Task>? AppendedCallbackAsync { get; set; }

	/// <summary>Helper method for verifying invariants in tests.</summary>
	protected override async Task ValidatedAsync()
	{
		await base.ValidatedAsync().ConfigureAwait(false);

		TestOutput.WriteLine(nameof(ValidatedAsync));
		ValidatedSemaphore.Release();

		if (ValidatedCallbackAsync is not null)
		{
			await ValidatedCallbackAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Helper method for verifying invariants in tests.</summary>
	protected override async Task ConflictedAsync()
	{
		await base.ConflictedAsync().ConfigureAwait(false);

		TestOutput.WriteLine(nameof(ConflictedAsync));
		ConflictedSemaphore.Release();

		if (ConflictedCallbackAsync is not null)
		{
			await ConflictedCallbackAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Helper method for verifying invariants in tests.</summary>
	protected override async Task AppendedAsync()
	{
		await base.AppendedAsync().ConfigureAwait(false);

		TestOutput.WriteLine(nameof(AppendedAsync));
		AppendedSemaphore.Release();

		if (AppendedCallbackAsync is not null)
		{
			await AppendedCallbackAsync().ConfigureAwait(false);
		}
	}

	public void Dispose()
	{
		ValidatedSemaphore.Dispose();
		ConflictedSemaphore.Dispose();
		AppendedSemaphore.Dispose();
	}
}
