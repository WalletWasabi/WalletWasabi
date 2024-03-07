using Avalonia.Controls;
using Avalonia.Input;

namespace WalletWasabi.Fluent.Controls;

public class ChatTextBox: TextBox
{
	protected override Type StyleKeyOverride => typeof(TextBox);

	protected override void OnKeyDown(KeyEventArgs e)
	{
		// Ignore Enter key unless Shift key is also pressed.
		if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Shift) == 0)
		{
			return;
		}

		base.OnKeyDown(e);
	}
}
