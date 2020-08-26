using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Gui.Behaviors
{
	public class ToggleButtonToolTipsBehavior : Behavior<ToggleButton>
	{
		private CompositeDisposable? Disposables { get; set; }

		public string OnTrueTooltipText { get; set; } = string.Empty;

		public string OnFalseTooltipText { get; set; } = string.Empty;

		protected override void OnAttached()
		{
			Disposables?.Dispose();

			Disposables = new CompositeDisposable
			{
				AssociatedObject?.GetObservable(ToggleButton.IsCheckedProperty).Subscribe(x =>
					{
						if(!x.HasValue) return;

						ToolTip.SetTip(AssociatedObject, x.Value ? OnTrueTooltipText : OnFalseTooltipText);
					})
			};
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
			Disposables = null;
		}
	}
}
