using System.Threading.Tasks;
using System.Collections.Generic;

namespace WalletWasabi.Services;

public sealed class ComposedDisposable : IDisposable
{
	private readonly List<IDisposable> _disposables = [];
	private bool _isDisposed = false;

	public ComposedDisposable Add(IDisposable disposable)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);
		_disposables.Add(disposable);
		return this;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (!_isDisposed)
		{
			if (disposing)
			{
				_disposables.Reverse();
				foreach (var disposable in _disposables)
				{
					disposable.Dispose();
				}

				_disposables.Clear();
			}

			_isDisposed = true;
		}
	}

	public void AddRange(IEnumerable<IDisposable> disposables)
	{
		_disposables.AddRange(disposables);
	}
}

public class ComposedAsyncDisposable : IAsyncDisposable
{
	private readonly List<IAsyncDisposable> _disposables = [];
	private bool _isDisposed = false;

	public void Add(IAsyncDisposable disposable)
	{
		ObjectDisposedException.ThrowIf(_isDisposed, this);
		_disposables.Add(disposable);
	}

	public async ValueTask DisposeAsync()
	{
		if (!_isDisposed)
		{
			_isDisposed = true;

			_disposables.Reverse();
			foreach (var disposable in _disposables)
			{
				await disposable.DisposeAsync().ConfigureAwait(false);
			}
		}
	}
}

public static class DisposableExtensions
{
	public static ComposedDisposable DisposeUsing(this IDisposable disposable, ComposedDisposable container)
	{
		container.Add(disposable);
		return container;
	}

	public static ComposedAsyncDisposable DisposeUsing(this IAsyncDisposable disposable,
		ComposedAsyncDisposable container)
	{
		container.Add(disposable);
		return container;
	}
}
