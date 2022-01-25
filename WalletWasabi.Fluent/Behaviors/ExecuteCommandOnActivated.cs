using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace WalletWasabi.Fluent.Behaviors;

public class ExecuteCommandOnActivated : DisposingBehavior<Control>
{
	public static readonly StyledProperty<ICommand?> CommandProperty =
		AvaloniaProperty.Register<ExecuteCommandOnActivated, ICommand?>(nameof(Command));

	public static readonly StyledProperty<bool> IsEnabledProperty =
		AvaloniaProperty.Register<ExecuteCommandOnActivated, bool>(nameof(IsEnabled), defaultValue: true);

	public ICommand? Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public bool IsEnabled
	{
		get => GetValue(IsEnabledProperty);
		set => SetValue(IsEnabledProperty, value);
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
					if (!IsEnabled)
					{
						return;
					}

					if (Command is { } cmd && cmd.CanExecute(default))
					{
						cmd.Execute(default);
					}
				})
				.DisposeWith(disposables);
		}
	}
}
