using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace WalletWasabi.Fluent.Behaviors;

public class ExecuteCommandOnActivatedBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<ICommand?> CommandProperty =
		AvaloniaProperty.Register<ExecuteCommandOnActivatedBehavior, ICommand?>(nameof(Command));

	public ICommand? Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			var mainWindow = lifetime.MainWindow;

			Observable
				.FromEventPattern(mainWindow, nameof(mainWindow.Activated))
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
