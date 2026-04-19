using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.BundledApps;

/// <summary>
/// Async wrapper class for <see cref="System.Diagnostics._process"/> class that implements <see cref="WaitForExitAsync(CancellationToken)"/>
/// to asynchronously wait for a process to exit.
/// </summary>
public class ProcessAsync : IDisposable
{
	private bool _disposed = false;

	public ProcessAsync(ProcessStartInfo startInfo) : this(new Process() { StartInfo = startInfo })
	{
	}

	internal ProcessAsync(Process process)
	{
		_process = process;
	}

	private readonly Process _process;

	/// <inheritdoc cref="_process.StartInfo"/>
	public ProcessStartInfo StartInfo => _process.StartInfo;

	/// <inheritdoc cref="_process.ExitCode"/>
	public int ExitCode => _process.ExitCode;

	/// <inheritdoc cref="_process.HasExited"/>
	public virtual bool HasExited => _process.HasExited;

	/// <inheritdoc cref="_process.Id"/>
	public int Id => _process.Id;

	/// <inheritdoc cref="_process.Handle"/>
	public virtual IntPtr Handle => _process.Handle;

	/// <inheritdoc cref="_process.StandardInput"/>
	public StreamWriter StandardInput => _process.StandardInput;

	/// <inheritdoc cref="_process.StandardOutput"/>
	public StreamReader StandardOutput => _process.StandardOutput;

	/// <inheritdoc cref="_process.Start()"/>
	public void Start()
	{
		_process.StartWithExceptionLogging();
	}

	/// <inheritdoc cref="_process.Kill(bool)"/>
	public virtual void Kill(bool entireProcessTree = false)
	{
		_process.Kill(entireProcessTree);
	}

	/// <summary>
	/// Waits until the process either finishes on its own or when user cancels the action.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see cref="Task"/>.</returns>
	public virtual async Task WaitForExitAsync(CancellationToken cancellationToken)
	{
		await _process.GracefulWaitForExitAsync(cancellationToken).ConfigureAwait(false);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_process.Dispose();
		}

		_disposed = true;
	}

	public virtual void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
