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
		Mock<ProcessAsync> mockProcess = new(MockBehavior.Strict, new ProcessStartInfo());
		mockProcess.Setup(p => p.WaitForExitAsync(It.IsAny<CancellationToken>(), It.IsAny<bool>()))
			.Returns((CancellationToken cancellationToken, bool killOnCancel) => Task.Delay(Timeout.Infinite, cancellationToken));
		mockProcess.Setup(p => p.HasExited).Returns(false);
		mockProcess.Setup(p => p.Kill(It.IsAny<bool>()));

		TestInhibitorClass psTask = new(TimeSpan.FromSeconds(10), DefaultReason, mockProcess.Object);

		// Task was started and as such it cannot be done yet.
		Assert.False(psTask.IsDone);

		// Explicitly ask to stop the task.
		await psTask.StopAsync();

		// Now the task must be finished.
		Assert.True(psTask.IsDone);

		// Prolong after exit must fail.
		Assert.False(psTask.Prolong(TimeSpan.FromSeconds(5)));

		mockProcess.VerifyAll();
	}

	public class TestInhibitorClass : BaseInhibitorTask
	{
		public TestInhibitorClass(TimeSpan period, string reason, ProcessAsync process)
			: base(period, reason, process)
		{
		}
	}
}
