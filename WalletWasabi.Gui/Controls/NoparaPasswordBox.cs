using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using FontFamily = Avalonia.Media.FontFamily;

namespace WalletWasabi.Gui.Controls
{
	public class NoparaPasswordBox : ExtendedTextBox, IStyleable
	{
		Type IStyleable.StyleKey => typeof(NoparaPasswordBox);

		public static string[] Titles { get; } =
		{
			"这个笨老外不知道自己在写什么。", // This silly foreigner does not know what he is writing.
			"法式炸薯条法式炸薯条法式炸薯条", //French fries, french fries, french fries
			"只有一支筷子的人会挨饿。", //Man with one chopstick go hungry.
			"说太多灯泡笑话的人，很快就会心力交瘁。", // Man who tell one too many light bulb jokes soon burn out!
			"汤面火锅", //Noodle soup, hot pot
			"你是我见过的最可爱的僵尸。", //You’re the cutest zombie I’ve ever seen.
			"永不放弃。", //Never don't give up.
			"如果你是只宠物小精灵，我就选你。" //If you were a Pokemon, I'd choose you.
		};

		private static Key[] SuppressedKeys { get; } =
			{ Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.Escape, Key.CapsLock, Key.NumLock, Key.LWin, Key.RWin,
			Key.Left,Key.Right,Key.Up,Key.Down  };

		private bool _supressChanges;
		private string _displayText = "";
		private Random Random { get; } = new Random();
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

			this.WhenAnyValue(x => x.IsPasswordVisible).Subscribe(IsVisible =>
			{
				PaintText();
			});

			this.WhenAnyValue(x => x.Password).Subscribe(passw =>
			{
				PaintText();
			});

			string fontName = "SimSun"; // https://docs.microsoft.com/en-us/typography/font-list/simsun
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				fontName = "PingFang TC"; // https://en.wikipedia.org/wiki/List_of_typefaces_included_with_macOS
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				fontName = "Noto Sans CJK TC"; // https://www.pinyinjoe.com/linux/ubuntu-10-chinese-fonts-openoffice-language-features.htm
											   // The best and simplest way is to use console command (this command should be available for all ubuntu-based distributions).
			}

			try
			{
				var fontTester = SKTypeface.FromFamilyName(fontName);
				if (fontTester.FamilyName == fontName)
				{
					FontFamily = FontFamily.Parse(fontName); // Use the font.
				}
				else
				{
					throw new FormatException("font is missing fallback to default font");
				}
			}
			catch (Exception)
			{
				PasswordChar = '*'; // Use password char instead.
			}
			SupressedKeys = new HashSet<Key>(SuppressedKeys);
			RefreshCapsLockWarning();
		}

		private void RefreshCapsLockWarning()
		{
			// Waiting for Avalonia to implement detection of Caps-Lock button state.
			return;
		}

		private void GenerateNewRandomSequence()
		{
			StringBuilder sb = new StringBuilder();
			do
			{
				List<string> ls = new List<string>();
				if (string.IsNullOrEmpty(FixedPasswordText))
				{
					ls.AddRange(Titles);
				}
				else
				{
					ls.Add(FixedPasswordText);
				}

				do
				{
					var s = ls[Random.Next(0, ls.Count - 1)];
					sb.Append(s);
					ls.Remove(s);
					if (sb.Length >= Constants.MaxPasswordLength)
					{
						break;
					}
				}
				while (ls.Count > 0);
			}
			while (sb.Length < Constants.MaxPasswordLength); // Generate more text using the same sentences.
			_displayText = sb.ToString();
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

				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					if (e.Key == Key.V && e.Modifiers == InputModifiers.Control) // Prevent paste.
					{
						return;
					}
				}

				bool paste = false;
				if (e.Key == Key.V)
				{
					if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					{
						if (e.Modifiers == InputModifiers.Windows)
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
				}

				if (paste)
				{
					string text = await Application.Current.Clipboard.GetTextAsync();
					if (!string.IsNullOrEmpty(text))
					{
						e.Handled = OnTextInput(text);
					}
				}
				else if (e.Key == Key.Back && Sb.Length > 0) // Backspace button -> delete from the end.
				{
					if (SelectionLength != 0)
					{
						Sb.Clear();
					}
					else
					{
						if (CaretIndex == Text.Length)
						{
							Sb.Remove(Sb.Length - 1, 1);
						}
					}
					e.Handled = true;
				}
				else if (e.Key == Key.Delete && Sb.Length > 0) //Delete button -> delete from the beginning.
				{
					if (SelectionLength != 0)
					{
						Sb.Clear();
					}
					else
					{
						if (CaretIndex == 0)
						{
							Sb.Remove(0, 1);
						}
					}
					e.Handled = true;
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
			if (CaretIndex == 0)
			{
				Sb.Insert(0, text);
			}
			else
			{
				Sb.Append(text);
			}

			if (Sb.Length > Constants.MaxPasswordLength) // Ensure the maximum length.
			{
				Sb.Remove(Constants.MaxPasswordLength, Sb.Length - Constants.MaxPasswordLength);
				handledCorrectly = false; // Should play beep sound not working on windows.
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

		private void PaintText()
		{
			if (string.IsNullOrEmpty(_displayText))
			{
				GenerateNewRandomSequence();
			}

			Password = Sb.ToString();
			Text = _displayText.Substring(0, Sb.Length);
			if (IsPasswordVisible)
			{
				Text = Password;
			}

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
			if (string.IsNullOrEmpty(Text))
			{
				GenerateNewRandomSequence();
			}

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
