using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace WalletWasabi.Fluent.ViewModels.Scheme;

public class SchemeEditorTextBox: TextBox
{
	private int _historyIndex = -1;

	protected override Type StyleKeyOverride => typeof(TextBox);

	public static readonly StyledProperty<ICommand?> CommandProperty =
		AvaloniaProperty.Register<Button, ICommand?>(nameof(Command), enableDataValidation: true);

	public static readonly StyledProperty<Collection<string>> CommandHistoryProperty =
		AvaloniaProperty.Register<SchemeEditorTextBox, Collection<string>>(nameof(CommandHistory), enableDataValidation: true);

	protected override void OnKeyDown(KeyEventArgs e)
	{
		// Shift is for special cases
		if ((e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
		{
			// Shift+Enter add a new line
			if (e.Key == Key.Enter)
			{
				if (Command is not null)
				{
					// Store non-empty commands in history
					var commandText = Text?.Trim();
					if (!string.IsNullOrWhiteSpace(commandText))
					{
						if (!CommandHistory.Contains(commandText))
						{
							CommandHistory.Add(commandText);
						}
					}

					_historyIndex = -1;
					Command.Execute(System.Reactive.Unit.Default);
					e.Handled = true;
					return;
				}
			}

			// Shift+Up recovers the previous entered command
			if (e.Key == Key.Up)
			{
				e.Handled = true;
				NavigateHistory(-1);
				return;
			}

			// Shift+Down recovers the next entered command
			if (e.Key == Key.Down)
			{
				e.Handled = true;
				NavigateHistory(1);
				return;
			}

		}
		else if (e.Key == Key.Escape)
		{
			Text = "";
			e.Handled = true;
			return;
		}

		base.OnKeyDown(e);
	}

	private void NavigateHistory(int direction)
	{
		if (CommandHistory.Count == 0)
		{
			return;
		}

		_historyIndex += direction;

		if (_historyIndex < -1)
		{
			_historyIndex = CommandHistory.Count -1;
		}
		else if (_historyIndex >= CommandHistory.Count)
		{
			_historyIndex = 0;
		}

		if (_historyIndex == -1)
		{
			Text = string.Empty;
		}
		else
		{
			Text = CommandHistory[_historyIndex];
		}

		CaretIndex = Text.Length;
	}

	public ICommand? Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public Collection<string> CommandHistory
	{
		get => GetValue(CommandHistoryProperty);
		set => SetValue(CommandHistoryProperty, value);
	}
}
