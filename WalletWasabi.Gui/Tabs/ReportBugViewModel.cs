using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class ReportBugViewModel : DocumentTabViewModel
	{
		public ReportBugViewModel() : base("Report Bug")
		{
		}

		public void CopySelection(TextBox textBox)
		{
			SetClipboardText(GetSelection(textBox));
		}

		private string GetSelection(TextBox textBox)
		{
			var text = textBox.Text;
			if (string.IsNullOrEmpty(text))
				return "";
			var selectionStart = textBox.SelectionStart;
			var selectionEnd = textBox.SelectionEnd;
			var start = Math.Min(selectionStart, selectionEnd);
			var end = Math.Max(selectionStart, selectionEnd);
			if (start == end || (textBox.Text?.Length ?? 0) < end)
			{
				return "";
			}
			return text.Substring(start, end - start);
		}

		private static void SetClipboardText(string text)
		{
			try
			{
				Application.Current.Clipboard.SetTextAsync(text).GetAwaiter().GetResult();
			}
			catch (Exception)
			{
				// Apparently this exception sometimes happens randomly.
				// The MS controls just ignore it, so we'll do the same.
			}
		}
	}
}
