using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Behaviors
{
	internal class PutCursorAtEndTextBoxBehavior: Behavior<TextBox>
	{
		private TextBox _textBox;

		protected override void OnAttached()
		{
			base.OnAttached();

			_textBox = AssociatedObject as TextBox;

			if (_textBox == null)
			{
				return;
			}
			_textBox.PropertyChanged += TextInput;
	}

		protected override void OnDetaching()
		{
			if (_textBox == null)
			{
				return;
			}
			_textBox.PropertyChanged -= TextInput;

			base.OnDetaching();
		}

		private void TextInput(object sender, AvaloniaPropertyChangedEventArgs args)
		{
			if(args.Property.Name == "Text")
				Task.Delay(100).ContinueWith(x=>
					Dispatcher.UIThread.InvokeAsync(()=>_textBox.CaretIndex = _textBox.Text.Length + 1));
		}
	} 
}