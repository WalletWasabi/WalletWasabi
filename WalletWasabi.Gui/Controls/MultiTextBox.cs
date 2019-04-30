using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Controls
{
	public class MultiTextBox : ExtendedTextBox, IStyleable
	{
		Type IStyleable.StyleKey => typeof(MultiTextBox);
		private CancellationTokenSource CancelClipboardNotification { get; set; }
		private CompositeDisposable Disposables { get; set; }

		public static readonly StyledProperty<bool> ClipboardNotificationVisibleProperty =
		AvaloniaProperty.Register<MultiTextBox, bool>(nameof(ClipboardNotificationVisible), defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<double> ClipboardNotificationOpacityProperty =
		AvaloniaProperty.Register<MultiTextBox, double>(nameof(ClipboardNotificationOpacity), defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<ReactiveCommand<Unit, Unit>> CopyToClipboardCommandProperty =
		AvaloniaProperty.Register<MultiTextBox, ReactiveCommand<Unit, Unit>>(nameof(CopyToClipboardCommand), defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<bool> PasteOnClickProperty =
		AvaloniaProperty.Register<MultiTextBox, bool>(nameof(PasteOnClick), defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<bool> CopyOnClickProperty =
		AvaloniaProperty.Register<MultiTextBox, bool>(nameof(CopyOnClick), defaultBindingMode: BindingMode.TwoWay);

		public static readonly StyledProperty<bool> TextVisibleProperty =
		AvaloniaProperty.Register<MultiTextBox, bool>(nameof(TextVisible), defaultBindingMode: BindingMode.TwoWay);

		public bool ClipboardNotificationVisible
		{
			get => GetValue(ClipboardNotificationVisibleProperty);
			set => SetValue(ClipboardNotificationVisibleProperty, value);
		}

		public double ClipboardNotificationOpacity
		{
			get => GetValue(ClipboardNotificationOpacityProperty);
			set => SetValue(ClipboardNotificationOpacityProperty, value);
		}

		public bool PasteOnClick
		{
			get => GetValue(PasteOnClickProperty);
			set => SetValue(PasteOnClickProperty, value);
		}

		public bool CopyOnClick
		{
			get => GetValue(CopyOnClickProperty);
			set => SetValue(CopyOnClickProperty, value);
		}

		public bool TextVisible
		{
			get => GetValue(TextVisibleProperty);
			set => SetValue(TextVisibleProperty, value);
		}

		public ReactiveCommand<Unit, Unit> CopyToClipboardCommand { get; }

		public MultiTextBox()
		{
			ClipboardNotificationVisible = false;
			ClipboardNotificationOpacity = 0;

			Disposables = new CompositeDisposable();

			CopyToClipboardCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				await CopyToClipboardAsync();
			});
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);
			var text = e.NameScope.Get<TextPresenter>("PART_TextPresenter");
			var border = e.NameScope.Get<Border>("border");

			Observable.FromEventPattern(text, nameof(text.PointerPressed))
				.Merge(Observable.FromEventPattern(border, nameof(text.PointerPressed)))
				.Subscribe(async _ =>
				{
					await OnClickedAsync();
				}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.ClipboardNotificationVisible).Subscribe(visible =>
			{
				TextVisible = !visible;
			});
		}

		public async Task CopyToClipboardAsync()
		{
			try
			{
				CancelClipboardNotification?.Cancel();
				while (CancelClipboardNotification != null)
				{
					await Task.Delay(50);
				}
				CancelClipboardNotification = new CancellationTokenSource();

				var cancelToken = CancelClipboardNotification.Token;
				if (string.IsNullOrEmpty(Text))
				{
					return;
				}

				await Application.Current.Clipboard.SetTextAsync(Text);
				cancelToken.ThrowIfCancellationRequested();

				ClipboardNotificationVisible = true;
				ClipboardNotificationOpacity = 1;

				await Task.Delay(1000, cancelToken);
				ClipboardNotificationOpacity = 0;
				await Task.Delay(1000, cancelToken);
				ClipboardNotificationVisible = false;
			}
			catch (Exception ex) when (ex is OperationCanceledException
									|| ex is TaskCanceledException
									|| ex is TimeoutException)
			{
				Logging.Logger.LogTrace<MultiTextBox>(ex);
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<MultiTextBox>(ex);
			}
			finally
			{
				CancelClipboardNotification?.Dispose();
				CancelClipboardNotification = null;
			}
		}

		public async Task PasteFromClipboardAsync()
		{
			try
			{
				var text = await Application.Current.Clipboard.GetTextAsync();
				Text = text;
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<MultiTextBox>(ex);
			}
		}

		private async Task OnClickedAsync()
		{
			try
			{
				if (Global.UiConfig?.Autocopy is true)
				{
					if (CopyOnClick)
					{
						await CopyToClipboardAsync();
					}
					if (PasteOnClick)
					{
						await PasteFromClipboardAsync();
					}
				}
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<MultiTextBox>(ex);
			}
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);

			Disposables?.Dispose();
			Disposables = null;
		}
	}
}
