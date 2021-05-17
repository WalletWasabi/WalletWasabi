using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors
{
	public class TopLevelLoadedBehavior : AttachedToVisualTreeBehavior<Visual>
	{
		private IDisposable? _disposable;
		public static readonly StyledProperty<bool> IsLoadedProperty =
			AvaloniaProperty.Register<TopLevelLoadedBehavior, bool>(nameof(IsLoaded), defaultBindingMode: BindingMode.TwoWay);

		public bool IsLoaded
		{
			get => GetValue(IsLoadedProperty);
			set => SetValue(IsLoadedProperty, value);
		}

		private void TopLevelOnOpened(object? sender, EventArgs e)
		{
			IsLoaded = true;
		}

		protected override void OnAttachedToVisualTree()
		{
			if (AssociatedObject.GetVisualRoot() is TopLevel topLevel)
			{
				topLevel.Opened += TopLevelOnOpened;

				_disposable = Disposable.Create(() => { topLevel.Opened -= TopLevelOnOpened; });
			}
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();
			_disposable?.Dispose();
		}
	}
}