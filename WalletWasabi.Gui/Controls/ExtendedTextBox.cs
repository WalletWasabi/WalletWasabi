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
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls
{
	public class ExtendedTextBox : TextBox, IStyleable
	{
		private MenuItem _pasteItem = null;

		public ExtendedTextBox()
		{
			CopyCommand = ReactiveCommand.Create(async () =>
			{
				await CopyAsync();
			});

			PasteCommand = ReactiveCommand.Create(async () =>
			{
				await PasteAsync();
			});

			this.GetObservable(IsReadOnlyProperty).Subscribe(isReadOnly =>
			{
				if (ContextMenu is null) return;
				var items = ContextMenu.Items as Avalonia.Controls.Controls;

				if (isReadOnly)
				{
					if (items.Contains(_pasteItem))
					{
						items.Remove(_pasteItem);
						_pasteItem = null;
					}
				}
				else
				{
					if (!items.Contains(_pasteItem))
					{
						CreatePasteItem();
						items.Add(_pasteItem);
					}
				}
			});
		}

		Type IStyleable.StyleKey => typeof(TextBox);

		private ReactiveCommand CopyCommand { get; }
		private ReactiveCommand PasteCommand { get; }

		private async Task PasteAsync()
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

		private async Task CopyAsync()
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

		protected virtual bool IsCopyEnabled => true;

		private static readonly Geometry CopyIcon = Geometry.Parse(
				"M19,21H8V7H19M19,5H8A2,2 0 0,0 6,7V21A2,2 0 0,0 8,23H19A2,2 0 0,0 21,21V7A2,2 0 0,0 19,5M16,1H4A2,2 0 0,0 2,3V17H4V3H16V1Z");

		private static readonly Geometry PasteIcon = Geometry.Parse(
				@"M19,20H5V4H7V7H17V4H19M12,2A1,1 0 0,1 13,3A1,1 0 0,1 12,4A1,1 0 0,1 11,3A1,1 0 0,1 12,2M19,2H14.82C14.4,0.84
				13.3,0 12,0C10.7,0 9.6,0.84 9.18,2H5A2,2 0 0,0 3,4V20A2,2 0 0,0 5,22H19A2,2 0 0,0 21,20V4A2,2 0 0,0 19,2Z");

		private static DrawingPresenter GetCopyPresenter()
		{
			return new DrawingPresenter
			{
				Drawing = new GeometryDrawing
				{
					Brush = Brushes.LightGray,
					Geometry = CopyIcon
				},
				Width = 16,
				Height = 16
			};
		}

		private static DrawingPresenter GetPastePresenter()
		{
			return new DrawingPresenter
			{
				Drawing = new GeometryDrawing
				{
					Brush = Brushes.LightGray,
					Geometry = PasteIcon
				},
				Width = 16,
				Height = 16,
			};
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			ContextMenu = new ContextMenu
			{
				DataContext = this,
				Items = new Avalonia.Controls.Controls()
			};

			var menuItems = (ContextMenu.Items as Avalonia.Controls.Controls);
			if (IsCopyEnabled)
			{
				menuItems.Add(new MenuItem { Header = "Copy", Command = CopyCommand, Icon = GetCopyPresenter() });
			}

			if (!IsReadOnly)
			{
				CreatePasteItem();
				menuItems.Add(_pasteItem);
			}
		}

		private void CreatePasteItem()
		{
			if (_pasteItem != null) return;
			_pasteItem = new MenuItem { Header = "Paste", Command = PasteCommand, Icon = GetPastePresenter() };
		}
	}
}
