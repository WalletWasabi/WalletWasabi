using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.Controls
{
	public class NoparaPasswordBox : ExtendedTextBox, IStyleable
	{
		Type IStyleable.StyleKey => typeof(NoparaPasswordBox);

		private readonly char ReplacementChar = '*';

		private static Key[] SuppressedKeys { get; } =
		{
			Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.Escape, Key.CapsLock, Key.NumLock, Key.LWin, Key.RWin,
			Key.Left, Key.Right, Key.Up, Key.Down, Key.Enter
		};

		private bool _supressChanges;
		private StringBuilder Sb { get; } = new StringBuilder();
		private HashSet<Key> SupressedKeys { get; }
		private Button _presenter;

		public int SelectionLength => Math.Abs(SelectionEnd - SelectionStart);

		public static readonly StyledProperty<string> PasswordProperty =
			AvaloniaProperty.Register<NoparaPasswordBox, string>(nameof(Password), defaultBindingMode: BindingMode.TwoWay);

		public string Password
		{
			get => GetValue(PasswordProperty);
			set => SetValue(PasswordProperty, value);
		}

		public static readonly StyledProperty<string> FixedPasswordTextProperty =
			AvaloniaProperty.Register<NoparaPasswordBox, string>(nameof(FixedPasswordText), defaultBindingMode: BindingMode.TwoWay);

		public string FixedPasswordText
		{
			get => GetValue(FixedPasswordTextProperty);
			set => SetValue(FixedPasswordTextProperty, value);
		}

		public static readonly StyledProperty<bool> IsPasswordVisibleProperty =
			AvaloniaProperty.Register<NoparaPasswordBox, bool>(nameof(IsPasswordVisible), defaultBindingMode: BindingMode.TwoWay);

		public bool IsPasswordVisible
		{
			get => GetValue(IsPasswordVisibleProperty);
			set => SetValue(IsPasswordVisibleProperty, value);
		}

		public static readonly StyledProperty<string> WarningMessageProperty =
			AvaloniaProperty.Register<NoparaPasswordBox, string>(nameof(WarningMessage), defaultBindingMode: BindingMode.TwoWay);

		public string WarningMessage
		{
			get => GetValue(WarningMessageProperty);
			set => SetValue(WarningMessageProperty, value);
		}

		protected override bool IsCopyEnabled => false;

		public NoparaPasswordBox()
		{
			this.GetObservable(PasswordProperty).Subscribe(x =>
			{
				Password = x;
				if (string.IsNullOrEmpty(x)) // Clean the password box.
				{
					Sb.Clear();
				}
				else
				{
					Sb.Clear();
					Sb.Append(Password);
				}
			});

			this.GetObservable(FixedPasswordTextProperty).Subscribe(x =>
			{
				FixedPasswordText = x;
			});

			this.GetObservable(IsPasswordVisibleProperty).Subscribe(x =>
			{
				IsPasswordVisible = x;
			});

			this.WhenAnyValue(x => x.IsPasswordVisible).Subscribe(_ =>
			{
				PaintText();
			});

			this.WhenAnyValue(x => x.Password).Subscribe(_ =>
			{
				PaintText();
			});

			SupressedKeys = new HashSet<Key>(SuppressedKeys);
			RefreshCapsLockWarning();
		}

		private void RefreshCapsLockWarning()
		{
			// Waiting for Avalonia to implement detection of Caps-Lock button state.
			return;
		}

		protected override async void OnKeyDown(KeyEventArgs e)
		{
			try
			{
				if (e.Key == Key.Capital || e.Key == Key.CapsLock) // On windows Caps-Lock is Key.Capital.
				{
					RefreshCapsLockWarning();
				}
				if (SupressedKeys.Contains(e.Key))
				{
					return;
				}
				// Prevent copy.
				if ((e.Key == Key.C || e.Key == Key.X || e.Key == Key.Insert) && (e.Modifiers == InputModifiers.Control || e.Modifiers == InputModifiers.Windows))
				{
					return;
				}

				if (e.Key == Key.V)
				{
					bool paste = false;

					if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					{
						if (e.Modifiers == InputModifiers.Control) // Prevent paste.
						{
							return;
						}
						else if (e.Modifiers == InputModifiers.Windows)
						{
							paste = true;
						}
					}
					else
					{
						if (e.Modifiers == InputModifiers.Control)
						{
							paste = true;
						}
					}

					if (paste)
					{
						string text = await Application.Current.Clipboard.GetTextAsync();
						if (!string.IsNullOrEmpty(text))
						{
							e.Handled = OnTextInput(text);
						}

						return;
					}
				}

				if (Sb.Length > 0)
				{
					if (e.Key == Key.Back) // Backspace button -> delete from the end.
					{
						if (SelectionLength == 0)
						{
							if (CaretIndex == Text.Length)
							{
								Sb.Remove(Sb.Length - 1, 1);
							}
						}
						else
						{
							Sb.Clear();
						}
						e.Handled = true;
					}
					else if (e.Key == Key.Delete) //Delete button -> delete from the beginning.
					{
						if (SelectionLength == 0)
						{
							if (CaretIndex == 0)
							{
								Sb.Remove(0, 1);
							}
						}
						else
						{
							Sb.Clear();
						}
						e.Handled = true;
					}
				}
				else
				{
					if (SelectionLength != 0)
					{
						Sb.Clear();
					}

					base.OnKeyDown(e);
				}
				PaintText();
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<NoparaPasswordBox>(ex);
			}
		}

		protected override void OnTextInput(TextInputEventArgs e)
		{
			e.Handled = OnTextInput(e.Text);
		}

		/// <summary>
		/// All text input operation (keydown/paste/delete) should call this.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		private bool OnTextInput(string text)
		{
			if (_supressChanges)
			{
				return true; // Avoid recursive calls.
			}

			bool handledCorrectly = true;
			if (SelectionLength != 0)
			{
				Sb.Clear();
				_supressChanges = true; // Avoid recursive calls.
				SelectionStart = SelectionEnd = CaretIndex = 0;
				_supressChanges = false;
			}
			if (PasswordHelper.IsTooLong(Sb.Length + text.Length)) // Do not allow insert that would be too long.
			{
				handledCorrectly = false;
				_ = DisplayWarningAsync(PasswordHelper.PasswordTooLongMessage);
			}
			else if (CaretIndex == 0)
			{
				Sb.Insert(0, text);
			}
			else
			{
				Sb.Append(text);
			}

			if (handledCorrectly && PasswordHelper.IsTooLong(Sb.Length)) // We should not get here, ensure the maximum length.
			{
				PasswordHelper.IsTooLong(Sb.ToString(), out string limitedPassword);
				Sb.Clear();
				Sb.Append(limitedPassword);
				handledCorrectly = false; // Should play beep sound not working on windows.
				_ = DisplayWarningAsync(PasswordHelper.PasswordTooLongMessage);
			}

			PaintText();
			return handledCorrectly;
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			if (_supressChanges)
			{
				return;
			}

			if (e.Property.Name == nameof(CaretIndex))
			{
				CaretIndex = Text.Length;
			}
			else if (e.Property.Name == nameof(SelectionStart) || e.Property.Name == nameof(SelectionEnd))
			{
				var len = SelectionEnd - SelectionStart;
				if (len > 0)
				{
					SelectionStart = 0;
					SelectionEnd = Text.Length;
				}
				else
				{
					SelectionStart = SelectionEnd = 0;
					CaretIndex = Text.Length;
				}
			}
			else
			{
				base.OnPropertyChanged(e);
			}
		}

		private async Task DisplayWarningAsync(string message)
		{
			WarningMessage = message;
			await Task.Delay(2000); // 2 seconds
			WarningMessage = "";
		}

		private void PaintText()
		{
			var password = Sb.ToString();

			if (PasswordHelper.IsTrimable(password, out string trimmedPassword))
			{
				password = trimmedPassword;
				Sb.Clear();
				Sb.Append(password);
				_ = DisplayWarningAsync(PasswordHelper.TrimmedMessage);
			}

			Text = IsPasswordVisible ? password : new string(ReplacementChar, password.Length);

			Password = Sb.ToString(); // Do not use Password instead of local variable. It is not changed immediately after this line.

			_supressChanges = true;
			try
			{
				// Text = Password; //for debugging
				CaretIndex = Text.Length;
			}
			finally
			{
				_supressChanges = false;
			}
		}

		protected override void OnGotFocus(GotFocusEventArgs e)
		{
			base.OnGotFocus(e);
			RefreshCapsLockWarning();
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);
			_presenter = e.NameScope.Get<Button>("PART_MaskedButton");

			_presenter.WhenAnyValue(x => x.IsPressed)
				.Subscribe(isPressed =>
				{
					IsPasswordVisible = isPressed;
				});
		}
	}
}
