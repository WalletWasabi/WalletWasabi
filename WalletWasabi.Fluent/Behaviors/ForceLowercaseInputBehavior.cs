using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class ForceLowercaseInputBehavior : AttachedToVisualTreeBehavior<TextBox>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		disposable.Add(new TextEntryOverrider(AssociatedObject, s => s.ToLowerInvariant()));
	}

	private class TextEntryOverrider : IDisposable
	{
		private readonly IDisposable _entryReplacer;
		private readonly IDisposable _pasteReplacer;
		private readonly TextBox _target;
		private readonly Func<string, string> _convert;

		public TextEntryOverrider(TextBox target, Func<string, string> convert)
		{
			_target = target;
			_convert = convert;
			_entryReplacer = CreateTextEntryReplacer();
			_pasteReplacer = CreatePasteReplacer();
		}
		
		private IDisposable CreatePasteReplacer()
		{
			return Observable
				.FromEventPattern<RoutedEventArgs>(_target, nameof(TextBox.PastingFromClipboard))
				.Select(x => x.EventArgs)
				.Do(r => r.Handled = true)
				.ToSignal()
				.SelectMany(_ => GetClipboardTextAsync())
				.Do(text => InsertText(_target, _convert(text)))
				.Subscribe();
		}

		private void InsertText(TextBox target, string text)
		{
			var left = target.Text[..target.SelectionStart];
			var right = target.Text[target.SelectionEnd..];
			target.Text = left + text + right;
			var finalPosition = left.Length + text.Length;
			PlaceCaretIn(finalPosition);
		}

		private void PlaceCaretIn(int finalPosition)
		{
			_target.ClearSelection();
			_target.CaretIndex = finalPosition;
			_target.SelectionStart = finalPosition;
			_target.SelectionEnd = finalPosition;
		}

		private static async Task<string> GetClipboardTextAsync()
		{
			if (Application.Current is {Clipboard: { } clipboard})
			{
				return await clipboard.GetTextAsync();
			}

			return "";
		}

		private IDisposable CreateTextEntryReplacer()
		{
			return _target
				.AddDisposableHandler(
					InputElement.TextInputEvent,
					(_, args) => args.Text = _convert(args.Text ?? ""),
					RoutingStrategies.Tunnel);
		}

		public void Dispose()
		{
			_entryReplacer.Dispose();
			_pasteReplacer.Dispose();
		}
	}
}