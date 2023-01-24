using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Banning;

public class CoinVerifyItem : IDisposable
{
	private bool _disposedValue;

	public CoinVerifyItem()
	{
	}

	public DateTimeOffset ScheduleTime { get; } = DateTimeOffset.UtcNow;
	private TaskCompletionSource<CoinVerifyResult> TaskCompletionSource { get; } = new();
	private CancellationTokenSource AbortCts { get; } = new();
	public Task<CoinVerifyResult> Task => TaskCompletionSource.Task;
	public bool IsCancellationRequested => AbortCts.IsCancellationRequested;
	public CancellationToken Token => AbortCts.Token;

	public void Cancel()
	{
		AbortCts.Cancel();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				AbortCts.Cancel();
				AbortCts.Dispose();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public void SetResult(CoinVerifyResult coinVerifyResult)
	{
		TaskCompletionSource.SetResult(coinVerifyResult);
	}

	public void ThrowIfCancellationRequested()
	{
		AbortCts.Token.ThrowIfCancellationRequested();
	}
}
