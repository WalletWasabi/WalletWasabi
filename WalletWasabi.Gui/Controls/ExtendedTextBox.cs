using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Controls
{
	public class ExtendedTextBox : TextBox, IStyleable
	{
		public ExtendedTextBox()
		{
			CopyCommand = ReactiveCommand.Create(() =>
			{
				CopyAsync();
			});

			PasteCommand = ReactiveCommand.Create(() =>
			{
				PasteAsync();
			}, this.GetObservable(IsReadOnlyProperty).Select(x => !x));
		}

		Type IStyleable.StyleKey => typeof(TextBox);

		private ReactiveCommand CopyCommand { get; }
		private ReactiveCommand PasteCommand { get; }

		private async void PasteAsync()
		{
			var text = await ((IClipboard)AvaloniaLocator.Current.GetService(typeof(IClipboard))).GetTextAsync();

			if (text == null)
			{
				return;
			}

			OnTextInput(new TextInputEventArgs { Text = text });
		}

		private string GetSelection()
		{
			var text = Text;

			if (string.IsNullOrEmpty(text))
				return "";

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

		private async void CopyAsync()
		{
			var selection = GetSelection();

			if (string.IsNullOrWhiteSpace(selection))
			{
				selection = Text;
			}

			if (!string.IsNullOrWhiteSpace(selection))
			{
				await ((IClipboard)AvaloniaLocator.Current.GetService(typeof(IClipboard)))
					.SetTextAsync(selection);
			}
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			ContextMenu = new ContextMenu
			{
				DataContext = this,
			};

			ContextMenu.Items = new Avalonia.Controls.Controls
			{
				new MenuItem { Header = "Copy", Command = CopyCommand },
				new MenuItem { Header = "Paste", Command = PasteCommand}
			};
		}
	}
}
