using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Controls;

public partial class CopyablePasswordTextBox : TextBox
{
	public static readonly DirectProperty<CopyablePasswordTextBox, bool> CanCutModifiedProperty =
		AvaloniaProperty.RegisterDirect<CopyablePasswordTextBox, bool>(
			nameof(CanCutModified),
			o => o.CanCutModified);

	public static readonly DirectProperty<CopyablePasswordTextBox, bool> CanCopyModifiedProperty =
		AvaloniaProperty.RegisterDirect<CopyablePasswordTextBox, bool>(
			nameof(CanCopyModified),
			o => o.CanCopyModified);

	public static readonly DirectProperty<CopyablePasswordTextBox, bool> CanPasteModifiedProperty =
		AvaloniaProperty.RegisterDirect<CopyablePasswordTextBox, bool>(
			nameof(CanPasteModified),
			o => o.CanPasteModified);

	private bool _canCutModified;
	private bool _canCopyModified;
	private bool _canPasteModified;

	protected override Type StyleKeyOverride => typeof(TextBox);

	public bool CanCutModified
	{
		get => _canCutModified;
		private set => SetAndRaise(CanCutModifiedProperty, ref _canCutModified, value);
	}

	public bool CanCopyModified
	{
		get => _canCopyModified;
		private set => SetAndRaise(CanCopyProperty, ref _canCopyModified, value);
	}

	public bool CanPasteModified
	{
		get => _canPasteModified;
		private set => SetAndRaise(CanPasteProperty, ref _canPasteModified, value);
	}

	private string GetSelection()
	{
		var text = Text;

		if (string.IsNullOrEmpty(text))
		{
			return "";
		}

		var selectionStart = SelectionStart;
		var selectionEnd = SelectionEnd;
		var start = Math.Min(selectionStart, selectionEnd);
		var end = Math.Max(selectionStart, selectionEnd);

		if (start == end || (Text?.Length ?? 0) < end)
		{
			return "";
		}

		return text.Substring(start, end - start);
	}

	private void UpdateCommandStates()
	{
		var text = GetSelection();
		var isSelectionNullOrEmpty = string.IsNullOrEmpty(text);
		CanCopyModified = !isSelectionNullOrEmpty;
		CanCutModified = !isSelectionNullOrEmpty && !IsReadOnly;
		CanPasteModified = !IsReadOnly;
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		var handled = false;
		var keymap = Application.Current!.PlatformSettings!.HotkeyConfiguration;

		bool Match(List<KeyGesture> gestures) => gestures.Any(g => g.Matches(e));

		if (Match(keymap.Copy))
		{
			Copy();
			handled = true;
		}
		else if (Match(keymap.Cut))
		{
			Cut();
			handled = true;
		}
		else if (Match(keymap.Paste))
		{
			Paste();
			handled = true;
		}

		if (handled)
		{
			e.Handled = true;
		}
		else
		{
			base.OnKeyDown(e);
		}
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == TextProperty)
		{
			UpdateCommandStates();
		}
		else if (change.Property == SelectionStartProperty)
		{
			UpdateCommandStates();
		}
		else if (change.Property == SelectionEndProperty)
		{
			UpdateCommandStates();
		}
	}

	protected override void OnGotFocus(GotFocusEventArgs e)
	{
		base.OnGotFocus(e);

		UpdateCommandStates();
	}

	protected override void OnLostFocus(RoutedEventArgs e)
	{
		base.OnLostFocus(e);

		UpdateCommandStates();
	}
}
