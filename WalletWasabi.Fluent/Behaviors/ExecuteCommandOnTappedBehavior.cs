using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.Custom;
using System.Reactive.Disposables;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class ExecuteCommandOnTappedBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<ICommand?> CommandProperty =
		AvaloniaProperty.Register<ExecuteCommandOnTappedBehavior, ICommand?>(nameof(Command));

	public static readonly StyledProperty<object?> CommandParameterProperty =
		AvaloniaProperty.Register<ExecuteCommandOnTappedBehavior, object?>(nameof(CommandParameter));

	public ICommand? Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public object? CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		AssociatedObject.Tapped += OnTapped;

		return Disposable.Create(() =>
		{
			if (AssociatedObject != null)
			{
				AssociatedObject.Tapped -= OnTapped;
			}
		});
	}

	private void OnTapped(object? sender, TappedEventArgs e)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var visualRoot = AssociatedObject.GetVisualRoot() as Control;
		bool isMobile = visualRoot != null && visualRoot.Bounds.Width > 0 && visualRoot.Bounds.Width <= 600;

		if (isMobile && IsEnabled && Command != null && Command.CanExecute(CommandParameter))
		{
			Command.Execute(CommandParameter);
			e.Handled = true;
		}
	}
}
