using System.ComponentModel;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Helpers;

public class FlyoutCloseEnforcer : IDisposable
{
	private readonly FlyoutBase _flyout;
	private bool _isForcedOpen;

	public FlyoutCloseEnforcer(FlyoutBase flyout)
	{
		_flyout = flyout;
	}

	public bool IsForcedOpen
	{
		get => _isForcedOpen;
		set
		{
			if (_isForcedOpen == value)
			{
				return;
			}

			_isForcedOpen = value;

			if (_isForcedOpen)
			{
				_flyout.Closing += RejectClose;
			}
			else
			{
				_flyout.Closing -= RejectClose;
			}
		}
	}

	private static void RejectClose(object? sender, CancelEventArgs e)
	{
		e.Cancel = true;
	}

	public void Dispose()
	{
		if (!IsForcedOpen)
		{
			_flyout.Closing -= RejectClose;
		}
	}
}