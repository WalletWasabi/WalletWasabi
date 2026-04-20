using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.BundledApps;

/// <summary>
/// Async wrapper class for <see cref="System.Diagnostics._process"/> class that implements <see cref="GracefulWaitForExitAsync(CancellationToken)"/>
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

	/// <inheritdoc cref="Process.StartInfo"/>
	public ProcessStartInfo StartInfo => _process.StartInfo;

	/// <inheritdoc cref="Process.ExitCode"/>
	public int ExitCode => _process.ExitCode;

	/// <inheritdoc cref="Process.HasExited"/>
	public virtual bool HasExited => _process.HasExited;

	/// <inheritdoc cref="Process.Id"/>
	public int Id => _process.Id;

	/// <inheritdoc cref="Process.Handle"/>
	public virtual IntPtr Handle => _process.Handle;

	/// <inheritdoc cref="Process.StandardInput"/>
	public StreamWriter StandardInput => _process.StandardInput;

	/// <inheritdoc cref="Process.StandardOutput"/>
	public StreamReader StandardOutput => _process.StandardOutput;

	public void StartWithExceptionLogging()
	{
		_process.StartWithExceptionLogging();
	}

	/// <inheritdoc cref="Process.Kill(bool)"/>
	public virtual void Kill(bool entireProcessTree = false)
	{
		_process.Kill(entireProcessTree);
	}

	/// <summary>
	/// Waits until the process either finishes on its own or when user cancels the action.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see cref="Task"/>.</returns>
	public virtual async Task GracefulWaitForExitAsync(CancellationToken cancellationToken)
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
