using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using AvalonStudio.Extensibility.Theme;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls
{
	public class ExtendedTextBox : TextBox, IStyleable
	{
		private TextPresenter _presenter;
		private MenuItem _pasteItem = null;

		private Subject<string> _textPasted;

		public ExtendedTextBox()
		{
			Disposables = new CompositeDisposable();

			_textPasted = new Subject<string>();

			CopyCommand = ReactiveCommand.CreateFromTask(CopyAsync);

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					var pastedText = await PasteAsync();

					_textPasted.OnNext(pastedText);
				}
				catch (Exception exception)
				{
					_textPasted.OnError(exception);
				}
			});

			Observable
				.Merge(CopyCommand.ThrownExceptions)
				.Merge(PasteCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogWarning(ex));

			this.GetObservable(IsReadOnlyProperty).Subscribe(isReadOnly =>
				{
					if (ContextMenu is null)
					{
						return;
					}

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

		private CompositeDisposable Disposables { get; }

		public IObservable<string> TextPasted => _textPasted.AsObservable();

		Type IStyleable.StyleKey => typeof(TextBox);

		private ReactiveCommand<Unit, Unit> CopyCommand { get; }
		private ReactiveCommand<Unit, Unit> PasteCommand { get; }

		protected virtual bool IsCopyEnabled => true;

		private async Task<string> PasteAsync()
		{
			var text = await Application.Current.Clipboard.GetTextAsync();

			if (text is null)
			{
				return null;
			}

			OnTextInput(new TextInputEventArgs { Text = text });
			return text;
		}

		protected string GetSelection()
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

			return text[start..end];
		}

		protected virtual async Task CopyAsync()
		{
			var selection = GetSelection();

			if (string.IsNullOrWhiteSpace(selection))
			{
				selection = Text;
			}

			if (!string.IsNullOrWhiteSpace(selection))
			{
				await Application.Current.Clipboard.SetTextAsync(selection);
			}
		}

		private DrawingPresenter GetCopyPresenter()
		{
			if (ResourceNodeExtensions.TryFindResource(this, "Copy", out var rawIcon))
			{
				if (rawIcon is DrawingGroup icon)
				{
					return new DrawingPresenter() { Drawing = icon };
				}
			}

			return null;
		}

		private DrawingPresenter GetPastePresenter()
		{
			if (ResourceNodeExtensions.TryFindResource(this, "Paste", out var rawIcon))
			{
				if (rawIcon is DrawingGroup icon)
				{
					return new DrawingPresenter() { Drawing = icon };
				}
			}

			return null;
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			_presenter = e.NameScope.Get<TextPresenter>("PART_TextPresenter");

			ContextMenu = new ContextMenu
			{
				DataContext = this,
				Items = new Avalonia.Controls.Controls()
			};

			Observable.FromEventPattern(ContextMenu, nameof(ContextMenu.MenuClosed))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => Focus())
				.DisposeWith(Disposables);

			var menuItems = (ContextMenu.Items as Avalonia.Controls.Controls);
			if (IsCopyEnabled)
			{
				menuItems.Add(new MenuItem { Header = "Copy", Foreground = ColorTheme.CurrentTheme.Foreground, Command = CopyCommand, Icon = GetCopyPresenter() });
			}

			if (!IsReadOnly)
			{
				CreatePasteItem();
				menuItems.Add(_pasteItem);
			}
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);

			Disposables?.Dispose();
		}

		protected override void OnLostFocus(RoutedEventArgs e)
		{
			// Dispatch so that if there is a context menu, it can open before the selection gets cleared.
			Dispatcher.UIThread.PostLogException(() =>
			{
				if (ContextMenu != null && ContextMenu.IsOpen)
				{
					_presenter?.HideCaret();
				}
				else
				{
					base.OnLostFocus(e);
				}
			});
		}

		private void CreatePasteItem()
		{
			if (_pasteItem != null)
			{
				return;
			}

			_pasteItem = new MenuItem { Header = "Paste", Foreground = ColorTheme.CurrentTheme.Foreground, Command = PasteCommand, Icon = GetPastePresenter() };
		}
	}
}
