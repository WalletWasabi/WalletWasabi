using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using Avalonia.Media;

namespace WalletWasabi.Gui.Controls
{
	public class ExtendedTextBox : TextBox, IStyleable
	{
		private MenuItem _pasteItem;

		public ExtendedTextBox()
		{
			CopyCommand = ReactiveCommand.Create(() =>
			{
				CopyAsync();
			});

			PasteCommand = ReactiveCommand.Create(() =>
			{
				PasteAsync();
			});

			this.GetObservable(IsReadOnlyProperty).Subscribe(ro =>
			{
				if (!(_pasteItem is null))
				{
					var items = ContextMenu.Items as Avalonia.Controls.Controls;

					if (ro)
					{
						if (items.Contains(_pasteItem))
						{
							items.Remove(_pasteItem);
						}
					}
					else
					{
						if (!items.Contains(_pasteItem))
						{
							items.Add(_pasteItem);
						}
					}
				}
			});
		}

		Type IStyleable.StyleKey => typeof(TextBox);

		private ReactiveCommand CopyCommand { get; }
		private ReactiveCommand PasteCommand { get; }

		private async void PasteAsync()
		{
			var text = await ((IClipboard)AvaloniaLocator.Current.GetService(typeof(IClipboard))).GetTextAsync();

			if (text is null)
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
			var pastePresenter = new DrawingPresenter
			{
				Drawing = new GeometryDrawing
				{
					Brush = Brushes.LightGray,
					Geometry = Geometry.Parse(
						@"M19,20H5V4H7V7H17V4H19M12,2A1,1 0 0,1 13,3A1,1 0 0,1 12,4A1,1 0 0,1 11,3A1,1 0 0,1 12,2M19,2H14.82C14.4,0.84
                    13.3,0 12,0C10.7,0 9.6,0.84 9.18,2H5A2,2 0 0,0 3,4V20A2,2 0 0,0 5,22H19A2,2 0 0,0 21,20V4A2,2 0 0,0 19,2Z")
				},
				Width = 16,
				Height = 16,
			};
			var copyPresenter = new DrawingPresenter
			{
				Drawing = new GeometryDrawing
				{
					Brush = Brushes.LightGray,
					Geometry = Geometry.Parse(
						"M19,21H8V7H19M19,5H8A2,2 0 0,0 6,7V21A2,2 0 0,0 8,23H19A2,2 0 0,0 21,21V7A2,2 0 0,0 19,5M16,1H4A2,2 0 0,0 2,3V17H4V3H16V1Z")
				},
				Width = 16,
				Height = 16
			};

			_pasteItem = new MenuItem { Header = "Paste", Command = PasteCommand, Icon = pastePresenter };

			ContextMenu.Items = new Avalonia.Controls.Controls
			{
				new MenuItem { Header = "Copy", Command = CopyCommand, Icon = copyPresenter }
			};

			if (!IsReadOnly)
			{
				(ContextMenu.Items as Avalonia.Controls.Controls).Add(_pasteItem);
			}
		}
	}
}
