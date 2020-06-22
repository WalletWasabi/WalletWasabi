using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Behaviors
{
	public class CloseWindowBehavior : Behavior<Window>
	{
		public static readonly StyledProperty<bool> CloseTriggerProperty =
			AvaloniaProperty.Register<Behavior, bool>(nameof(CloseTriggerProperty));

		public bool CloseTrigger
		{
			get => GetValue(CloseTriggerProperty);
			set => SetValue(CloseTriggerProperty, value);
		}

		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		protected override void OnAttached()
		{
			base.OnAttached();

			this.GetObservable(CloseTriggerProperty)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(closeTriggered =>
				{
					if (closeTriggered)
					{
						AssociatedObject.Close();
					}
				}).DisposeWith(Disposables);
		}

		protected override void OnDetaching()
		{
			Disposables?.Dispose();
			base.OnDetaching();
		}
	}
}
