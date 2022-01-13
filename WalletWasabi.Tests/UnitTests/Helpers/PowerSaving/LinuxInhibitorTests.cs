using Moq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers.PowerSaving;
using WalletWasabi.Microservices;
using Xunit;
using static WalletWasabi.Helpers.PowerSaving.LinuxInhibitorTask;

namespace WalletWasabi.Tests.UnitTests.Helpers.PowerSaving;

/// <summary>
/// Tests for <see cref="LinuxInhibitorTask"/> class.
/// </summary>
public class LinuxInhibitorTests
{
	private const string DefaultReason = "CJ is in progress";

	[Fact]
	public async Task TestAvailabilityAsync()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			bool isAvailable = await LinuxInhibitorTask.IsSystemdInhibitSupportedAsync();
			Assert.True(isAvailable);
		}
	}

	[Fact]
	public async Task CancelBehaviorAsync()
	{
		Mock<ProcessAsync> mockProcess = new(MockBehavior.Strict, new ProcessStartInfo());
		mockProcess.Setup(p => p.WaitForExitAsync(It.IsAny<CancellationToken>(), It.IsAny<bool>()))
			.Returns((CancellationToken cancellationToken, bool killOnCancel) => Task.Delay(Timeout.Infinite, cancellationToken));
		mockProcess.Setup(p => p.HasExited).Returns(false);
		mockProcess.Setup(p => p.Kill());

		LinuxInhibitorTask psTask = new(InhibitWhat.All, TimeSpan.FromSeconds(10), DefaultReason, mockProcess.Object);

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
}
