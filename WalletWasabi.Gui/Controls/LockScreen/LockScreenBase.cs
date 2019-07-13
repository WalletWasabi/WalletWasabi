using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.LockScreen
{
	public abstract class LockScreenBase : UserControl
	{
		private bool _currentState;
		private bool _isLocked;

		public static readonly DirectProperty<LockScreenBase, bool> IsLockedProperty =
			AvaloniaProperty.RegisterDirect<LockScreenBase, bool>(nameof(IsLocked),
																  o => o.IsLocked,
																  (o, v) => o.IsLocked = v);
		public bool IsLocked
		{
			get => _isLocked;
			set => SetAndRaise(IsLockedProperty, ref _isLocked, value);
		}

		public LockScreenBase()
		{
			this.WhenAnyValue(x => x.IsLocked)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(IsLockedChanged);
		}

		private void IsLockedChanged(bool isLocked)
		{
			if (isLocked == _currentState) return;

			_currentState = isLocked;

			if (isLocked)
			{
				this.IsHitTestVisible = true;
				DoLock();
			}
			else
			{
				this.IsHitTestVisible = false;
				DoUnlock();
			}
		}

		public abstract void DoLock();
		public abstract void DoUnlock();
	}
}
