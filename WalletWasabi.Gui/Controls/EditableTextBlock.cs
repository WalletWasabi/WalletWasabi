using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using System;

namespace WalletWasabi.Gui.Controls
{
	public class EditableTextBlock : TemplatedControl
	{
		private string _text;
		private string _editText;
		private TextBox _textBox;

		private DispatcherTimer EditClickTimer { get; }
		private IInputRoot _root;

		static EditableTextBlock()
		{
			PseudoClass<EditableTextBlock>(InEditModeProperty, ":editing");
		}

		public EditableTextBlock()
		{
			EditClickTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(500)
			};

			EditClickTimer.Tick += (sender, e) =>
				{
					EditClickTimer.Stop();

					if (IsFocused && !InEditMode)
					{
						EnterEditMode();
					}
				};

			this.GetObservable(TextProperty).Subscribe(t => EditText = t);

			this.GetObservable(InEditModeProperty).Subscribe(mode =>
				{
					if (mode && _textBox != null)
					{
						EnterEditMode();
					}
				});

			AddHandler(PointerPressedEvent,
				(sender, e) =>
				{
					EditClickTimer.Stop();

					if (!InEditMode)
					{
#pragma warning disable CS0618 // Type or member is obsolete
						if (e.ClickCount == 1 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsFocused)
#pragma warning restore CS0618 // Type or member is obsolete
						{
							EditClickTimer.Start();
						}
					}
					else
					{
						var hit = this.InputHitTest(e.GetPosition(this));
						if (hit is null)
						{
							ExitEditMode();
						}
						else
						{
							e.GetCurrentPoint(this).Pointer.Capture(_textBox);							
						}
					}
				},
				RoutingStrategies.Tunnel);

			AddHandler(PointerReleasedEvent,
				(sender, e) =>
				{
					if (InEditMode)
					{
						var hit = this.InputHitTest(e.GetPosition(this));
						if (hit is null)
						{
							ExitEditMode();
						}
						else
						{
							e.GetCurrentPoint(this).Pointer.Capture(_textBox);
						}
					}
				},
				RoutingStrategies.Tunnel);
		}

		public static readonly DirectProperty<EditableTextBlock, string> TextProperty =
			TextBlock.TextProperty.AddOwner<EditableTextBlock>(o => o.Text, (o, v) => o.Text = v, defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);

		[Content]
		public string Text
		{
			get => _text;
			set => SetAndRaise(TextProperty, ref _text, value);
		}

		public string EditText
		{
			get => _editText;
			set => SetAndRaise(EditTextProperty, ref _editText, value);
		}

		public static readonly DirectProperty<EditableTextBlock, string> EditTextProperty =
			AvaloniaProperty.RegisterDirect<EditableTextBlock, string>(nameof(EditText), o => o.EditText, (o, v) => o.EditText = v);

		public static readonly StyledProperty<bool> InEditModeProperty =
			AvaloniaProperty.Register<EditableTextBlock, bool>(nameof(InEditMode), defaultBindingMode: BindingMode.TwoWay);

		public bool InEditMode
		{
			get => GetValue(InEditModeProperty);
			set => SetValue(InEditModeProperty, value);
		}

		public static readonly StyledProperty<bool> ReadModeProperty =
			AvaloniaProperty.Register<EditableTextBlock, bool>(nameof(ReadMode), defaultValue: true, defaultBindingMode: BindingMode.TwoWay);

		public bool ReadMode
		{
			get => GetValue(ReadModeProperty);
			set => SetValue(ReadModeProperty, value);
		}

		public static readonly StyledProperty<bool> ReadOnlyModeProperty =
			AvaloniaProperty.Register<EditableTextBlock, bool>(nameof(ReadOnlyMode), defaultValue: true, defaultBindingMode: BindingMode.TwoWay);

		public bool ReadOnlyMode
		{
			get => GetValue(ReadOnlyModeProperty);
			set => SetValue(ReadOnlyModeProperty, value);
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			_textBox = e.NameScope.Find<TextBox>("PART_TextBox");

			if (InEditMode)
			{
				EnterEditMode();
			}
		}

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			_root = e.Root as IInputRoot;
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Enter:
					ExitEditMode();
					e.Handled = true;
					break;

				case Key.Escape:
					ExitEditMode(true);
					e.Handled = true;
					break;
			}

			base.OnKeyUp(e);
		}

		private void EnterEditMode()
		{
			if (!ReadOnlyMode)
			{
				EditText = Text;
				ReadMode = false;
				InEditMode = true;

#pragma warning disable CS0618 // Type or member is obsolete
				_root.MouseDevice.Capture(_textBox);
#pragma warning restore CS0618 // Type or member is obsolete
				_textBox.SelectionStart = 0;
				_textBox.SelectionEnd = Text.Length;
				_textBox.CaretIndex = Text.Length;

				Dispatcher.UIThread.InvokeAsync(() => _textBox.Focus());
			}
			else
			{
				InEditMode = false;
			}
		}

		private void ExitEditMode(bool restore = false)
		{
			if (!restore)
			{
				Text = EditText;
			}

			InEditMode = false;
			ReadMode = true;
#pragma warning disable CS0618 // Type or member is obsolete
			_root.MouseDevice.Capture(null);
#pragma warning restore CS0618 // Type or member is obsolete
		}
	}
}
