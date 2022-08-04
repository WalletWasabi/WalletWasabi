using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Helpers;

public class FlyoutShowController : IDisposable
{
	private readonly FlyoutBase _flyout;
	private readonly Control _parent;
	private bool _isForcedOpen;

	public FlyoutShowController(Control parent, FlyoutBase flyout)
	{
		_flyout = flyout;
		_parent = parent;
	}

	public void SetIsForcedOpen(bool value)
	{
		if (_isForcedOpen == value)
		{
			return;
		}

		_isForcedOpen = value;

		if (_isForcedOpen)
		{
			_flyout.Closing += RejectClose;
			_flyout.ShowAt(_parent);
		}
		else
		{
			_flyout.Closing -= RejectClose;
			_flyout.Hide();
		}
	}

	private static void RejectClose(object? sender, CancelEventArgs e)
	{
		e.Cancel = true;
	}

	public void Dispose()
	{
		_flyout.Closing -= RejectClose;
	}
}
