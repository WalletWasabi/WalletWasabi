using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace WalletWasabi.Fluent.ViewModels.Scheme;

public class CommandConsoleTextBox: TextBox
{
	private readonly List<string> _commandHistory = new();
	private int _historyIndex = -1;

	protected override Type StyleKeyOverride => typeof(TextBox);

	public static readonly StyledProperty<ICommand?> CommandProperty =
		AvaloniaProperty.Register<Button, ICommand?>(nameof(Command), enableDataValidation: true);

	protected override void OnKeyDown(KeyEventArgs e)
	{
		// Shift is for special cases
		if ((e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
		{
			// Shift+Enter add a new line
			if (e.Key == Key.Enter)
			{
				var text = Text;
				var pos = CaretIndex;
				Text = pos >= 0 && pos <= text.Length
					? text[..pos] + "\n" + text[pos..]
					: text + "\n";

				CaretIndex = pos + 1;
				return;
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
		else if (e.Key == Key.Enter)
		{
			if (Command is not null)
			{
				// Store non-empty commands in history
				if (!string.IsNullOrWhiteSpace(Text))
				{
					_commandHistory.Add(Text);
				}

				_historyIndex = -1;
				Command.Execute(System.Reactive.Unit.Default);
				e.Handled = true;
				return;
			}
		}

		base.OnKeyDown(e);
	}

	private void NavigateHistory(int direction)
	{
		if (_commandHistory.Count == 0)
		{
			return;
		}

		_historyIndex += direction;

		if (_historyIndex < -1)
		{
			_historyIndex = _commandHistory.Count -1;
		}
		else if (_historyIndex >= _commandHistory.Count)
		{
			_historyIndex = 0;
		}

		if (_historyIndex == -1)
		{
			Text = string.Empty;
		}
		else
		{
			Text = _commandHistory[_historyIndex];
		}

		CaretIndex = Text.Length;
	}

	public ICommand? Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}
}
