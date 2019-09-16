using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Behaviors
{
	public class BindSelectedTextBehavior : Behavior<TextBox>
	{
		private CompositeDisposable Disposables { get; } = new CompositeDisposable();

		public static readonly AvaloniaProperty<string> SelectedTextProperty =
			AvaloniaProperty.Register<BindSelectedTextBehavior, string>(nameof(SelectedText), defaultBindingMode: BindingMode.TwoWay);

		public string SelectedText
		{
			get => GetValue(SelectedTextProperty);
			set => SetValue(SelectedTextProperty, value);
		}

		private string GetSelection()
		{
			var text = AssociatedObject.Text;
			if (string.IsNullOrEmpty(text))
			{
				return "";
			}

			var selectionStart = AssociatedObject.SelectionStart;
			var selectionEnd = AssociatedObject.SelectionEnd;
			var start = Math.Min(selectionStart, selectionEnd);
			var end = Math.Max(selectionStart, selectionEnd);
			if (start == end || (AssociatedObject.Text?.Length ?? 0) < end)
			{
				return "";
			}
			return text.Substring(start, end - start);
		}

		protected override void OnAttached()
		{
			base.OnAttached();

			AssociatedObject.GetObservable(TextBox.SelectionStartProperty)
				.Merge(AssociatedObject.GetObservable(TextBox.SelectionEndProperty))
				.Subscribe(_ => SelectedText = GetSelection());
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}
	}
}
