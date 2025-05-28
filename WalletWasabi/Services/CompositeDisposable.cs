using System.Collections.Generic;
using System.Linq;

public class ComposedDisposable : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private bool _isDisposed = false;

    public void Add(IDisposable disposable)
    {
	    ObjectDisposedException.ThrowIf(_isDisposed, this);
		_disposables.Add(disposable);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
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
}

public static class DisposableExtensions
{
	public static ComposedDisposable DisposeUsing(this IDisposable disposable, ComposedDisposable container)
	{
		container.Add(disposable);
		return container;
	}
}
