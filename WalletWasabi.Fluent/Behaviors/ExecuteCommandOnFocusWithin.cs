using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
	public class ExecuteCommandOnFocusWithin : DisposingBehavior<Control>
	{
		public static readonly StyledProperty<ICommand?> CommandProperty =
			AvaloniaProperty.Register<ExecuteCommandOnFocusWithin, ICommand?>(nameof(Command));

		public ICommand? Command
		{
			get => GetValue(CommandProperty);
			set => SetValue(CommandProperty, value);
		}

		protected override void OnAttached(CompositeDisposable disposables)
		{
			AssociatedObject?.WhenAnyValue(x => x.IsKeyboardFocusWithin)
				.Where(x => x)
				.Subscribe(_ =>
				{
					if (Command is { } cmd && cmd.CanExecute(default))
					{
						cmd.Execute(default);
					}
				})
				.DisposeWith(disposables);
		}
	}
}
