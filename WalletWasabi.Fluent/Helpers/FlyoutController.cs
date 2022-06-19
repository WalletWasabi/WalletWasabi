using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Helpers;

public class FlyoutController : IDisposable
{
	private readonly FlyoutBase _flyout;
	private readonly Control _parent;
	private bool _isOpen;
	private readonly FlyoutCloseEnforcer _enforcer;

	public FlyoutController(FlyoutBase flyout, Control parent)
	{
		_flyout = flyout;
		_parent = parent;
		_enforcer = new FlyoutCloseEnforcer(flyout);
	}

	public bool IsOpen
	{
		get => _isOpen;
		set
		{
			if (_isOpen == value)
			{
				return;
			}

			ToggleFlyout(value);
			_isOpen = value;
		}
	}

	private void ToggleFlyout(bool isVisible)
	{
		if (isVisible)
		{
			ShowFlyout();
		}
		else
		{
			HideFlyout();
		}
	}

	private void HideFlyout()
	{
		_enforcer.IsForcedOpen = false;
		_flyout.Hide();
	}

	private void ShowFlyout()
	{
		_enforcer.IsForcedOpen = true;
		_flyout.ShowAt(_parent);
	}

	public void Dispose()
	{
		_enforcer.Dispose();
	}
}