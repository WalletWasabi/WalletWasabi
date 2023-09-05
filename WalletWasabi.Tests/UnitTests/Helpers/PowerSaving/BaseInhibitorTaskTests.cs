using Moq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers.PowerSaving;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers.PowerSaving;

/// <summary>
/// Tests for <see cref="BaseInhibitorTask"/> class.
/// </summary>
public class BaseInhibitorTaskTests
{
	private const string DefaultReason = "CJ is in progress";

	[Fact]
	public async Task CancelBehaviorAsync()
	{
		using MockProcessAsync mockProcess = new(new ProcessStartInfo());
		mockProcess.OnWaitForExitAsync = (cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
		mockProcess.OnHasExited = () => false;
		mockProcess.OnKill = b => { };

		TestInhibitorClass psTask = new(TimeSpan.FromSeconds(10), DefaultReason, mockProcess);

		// Task was started and as such it cannot be done yet.
		Assert.False(psTask.IsDone);

		// Explicitly ask to stop the task.
		await psTask.StopAsync();

		// Now the task must be finished.
		Assert.True(psTask.IsDone);

		// Prolong after exit must fail.
		Assert.False(psTask.Prolong(TimeSpan.FromSeconds(5)));
	}

	public class TestInhibitorClass : BaseInhibitorTask
	{
		public TestInhibitorClass(TimeSpan period, string reason, ProcessAsync process)
			: base(period, reason, process)
		{
		}
	}
}


public class MockProcessAsync : ProcessAsync
{
	public Func<CancellationToken, Task>? OnWaitForExitAsync { get; set; }
	public Func<bool>? OnHasExited { get; set; }
	public Func<nint>? OnHandle { get; set; }
	public Action<bool>? OnKill { get; set; }

	public MockProcessAsync(ProcessStartInfo startInfo) : base(startInfo)
	{
	}

	internal MockProcessAsync(Process process) : base(process)
	{
	}

	public override Task WaitForExitAsync(CancellationToken cancellationToken) =>
		OnWaitForExitAsync?.Invoke(cancellationToken)
		?? throw new NotImplementedException($"{nameof(WaitForExitAsync)} was invoked but never assigned.");

	public override bool HasExited =>
		OnHasExited?.Invoke()
		?? throw new NotImplementedException($"{nameof(HasExited)} was invoked but never assigned.");

	public override void Kill(bool entireProcessTree = false) =>
		OnKill?.Invoke(entireProcessTree);

	public override nint Handle =>
		OnHandle?.Invoke()
		?? throw new NotImplementedException($"{nameof(Handle)} was invoked but never assigned.");

}
