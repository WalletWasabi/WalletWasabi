using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Helpers;

public class FlyoutController : IDisposable
{
	private readonly FlyoutBase _flyout;
	private readonly Control _parent;
	private bool _isForcedOpen;
	private bool _isOpen;

	public FlyoutController(FlyoutBase flyout, Control parent)
	{
		_flyout = flyout;
		_parent = parent;
	}

	public void Dispose()
	{
		_flyout.Closing -= RejectClose;
	}

	public void SetIsOpen(bool value)
	{
		if (_isOpen == value)
		{
			return;
		}

		Toggle(value);

		_isOpen = value;
	}

	private static void RejectClose(object? sender, CancelEventArgs e)
	{
		e.Cancel = true;
	}

	private void Toggle(bool value)
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

		if (value)
		{
			_flyout.ShowAt(_parent);
		}
		else
		{
			_flyout.Hide();
		}
	}
}
