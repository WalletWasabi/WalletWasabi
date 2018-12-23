using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Gui.Controls
{
	public class NoparaPasswordBox : ExtendedTextBox
	{
		//public static readonly string[] Titles = { "你吃饭了吗？", "你多吃一点。", "慢慢吃。", " 慢走。", "我跟你讲。", "你去忙你的吧。" };
		public static readonly string[] Titles = { "alma körte banán", "második mondat", "harmadik mondat" };

		public string ContentText;
		public string DisplayText;
		private Random _random = new Random();

		protected override void OnTextInput(TextInputEventArgs e)
		{
			ContentText += e.Text;
			var index = (ContentText.Length - 1) % ContentText.Length;
			e.Text = DisplayText[index].ToString();
			base.OnTextInput(e);
		}

		private void GenerateNewRandomSequence()
		{
			var randomSequence = Titles.Select(_ => Titles[_random.Next(0, Titles.Length - 1)]).ToArray();
			DisplayText = string.Join(' ', randomSequence);
		}

		protected override void OnGotFocus(GotFocusEventArgs e)
		{
			GenerateNewRandomSequence();
			base.OnGotFocus(e);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property.ToString() == nameof(Text))
			{
				if (((string)e.NewValue).Length < ((string)e.OldValue).Length)
				{
				}
			}
			base.OnPropertyChanged(e);
		}
	}
}
