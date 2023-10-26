using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;

namespace WalletWasabi.Tests.UnitTests.Helpers.PowerSaving;

public class MockProcessAsync : ProcessAsync
{
	public MockProcessAsync(ProcessStartInfo startInfo) : base(startInfo)
	{
	}

	internal MockProcessAsync(Process process) : base(process)
	{
	}

	public Func<CancellationToken, Task>? OnWaitForExitAsync { get; set; }
	public Func<bool>? OnHasExited { get; set; }
	public Func<nint>? OnHandle { get; set; }
	public Action<bool>? OnKill { get; set; }

	public override bool HasExited =>
		OnHasExited?.Invoke()
		?? throw new NotImplementedException($"{nameof(HasExited)} was invoked but never assigned.");

	public override nint Handle =>
		OnHandle?.Invoke()
		?? throw new NotImplementedException($"{nameof(Handle)} was invoked but never assigned.");

	public override Task WaitForExitAsync(CancellationToken cancellationToken) =>
		OnWaitForExitAsync?.Invoke(cancellationToken)
		?? throw new NotImplementedException($"{nameof(WaitForExitAsync)} was invoked but never assigned.");

	public override void Kill(bool entireProcessTree = false) =>
		OnKill?.Invoke(entireProcessTree);
}
