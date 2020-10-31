using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// </summary>
	public class TagsBox : ItemsControl
	{
		public static readonly StyledProperty<bool> RestrictInputToSuggestionsProperty =
			AvaloniaProperty.Register<TagsBox, bool>(nameof(RestrictInputToSuggestions));

		public static readonly StyledProperty<int> ItemCountLimitProperty =
			AvaloniaProperty.Register<TagsBox, int>(nameof(ItemCountLimit));

		public static readonly StyledProperty<object> SelectedTagProperty =
			AvaloniaProperty.Register<TagsBox, object>(nameof(SelectedTag), defaultBindingMode: BindingMode.TwoWay);

		public static readonly DirectProperty<TagsBox, IEnumerable?> SuggestionsProperty =
			AvaloniaProperty.RegisterDirect<TagsBox, IEnumerable?>(
				nameof(Suggestions),
				o => o.Suggestions,
				(o, v) => o.Suggestions = v);

		public new static readonly DirectProperty<TagsBox, IEnumerable?> ItemsProperty =
			ItemsControl.ItemsProperty.AddOwnerWithDataValidation<TagsBox>(
				o => o.Items,
				(o, v) => o.Items = v,
				defaultBindingMode: BindingMode.TwoWay,
				enableDataValidation: true);

		private readonly CompositeDisposable CompositeDisposable = new CompositeDisposable();

		private bool _backspaceEmptyField1;
		private bool _backspaceEmptyField2;
		private bool _isFocused;
		private bool _isInputEnabled = true;
		private IEnumerable? _items;
		private IEnumerable? _suggestions;

		private AutoCompleteBox? _autoCompleteBox;

		public bool RestrictInputToSuggestions
		{
			get => GetValue(RestrictInputToSuggestionsProperty);
			set => SetValue(RestrictInputToSuggestionsProperty, value);
		}

		public object SelectedTag
		{
			get => GetValue(SelectedTagProperty);
			set => SetValue(SelectedTagProperty, value);
		}

		public int ItemCountLimit
		{
			get => GetValue(ItemCountLimitProperty);
			set => SetValue(ItemCountLimitProperty, value);
		}

		public new IEnumerable? Items
		{
			get => _items;
			set => SetAndRaise(ItemsProperty, ref _items, value);
		}

		public IEnumerable? Suggestions
		{
			get => _suggestions;
			set => SetAndRaise(SuggestionsProperty, ref _suggestions, value);
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			Presenter.ApplyTemplate();

			_autoCompleteBox = (Presenter.Panel as ConcatenatingWrapPanel)?.ConcatenatedChildren
				.OfType<AutoCompleteBox>().FirstOrDefault();

			if (_autoCompleteBox is null)
			{
				return;
			}

			_autoCompleteBox.KeyUp += OnKeyUp;
			_autoCompleteBox.TextChanged += OnTextChanged;
			_autoCompleteBox.DropDownClosed += OnDropDownClosed;
			_autoCompleteBox.GotFocus += OnOnAutoCompleteBoxGotFocus;
			_autoCompleteBox.LostFocus += OnAutoCompleteBoxLostFocus;

			_autoCompleteBox
				.AddDisposableHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel)
				.DisposeWith(CompositeDisposable);
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			if (_autoCompleteBox is { })
			{
				_autoCompleteBox.KeyUp -= OnKeyUp;
				_autoCompleteBox.TextChanged -= OnTextChanged;
				_autoCompleteBox.DropDownClosed -= OnDropDownClosed;
				_autoCompleteBox.GotFocus -= OnOnAutoCompleteBoxGotFocus;
				_autoCompleteBox.LostFocus -= OnAutoCompleteBoxLostFocus;
			}

			CompositeDisposable.Dispose();
			base.OnDetachedFromVisualTree(e);
		}

		private void CheckIsInputEnabled()
		{
			if (Items is IList x && ItemCountLimit > 0)
			{
				_isInputEnabled = x.Count < ItemCountLimit;
			}
		}

		private void OnAutoCompleteBoxLostFocus(object? _, RoutedEventArgs e)
		{
			PseudoClasses.Set(":focus", false);
		}

		private void OnOnAutoCompleteBoxGotFocus(object? _, GotFocusEventArgs e)
		{
			PseudoClasses.Set(":focus", true);
		}

		private void OnTextInput(object? sender, TextInputEventArgs e)
		{
			if (!(sender is AutoCompleteBox autoCompleteBox))
			{
				return;
			}

			if (!_isInputEnabled)
			{
				e.Handled = true;
				return;
			}

			if (RestrictInputToSuggestions &&
				Suggestions is IList<string> suggestions &&
				!suggestions.Any(x =>
					x.StartsWith(autoCompleteBox.SearchText ?? "", true, CultureInfo.CurrentCulture)))
			{
				e.Handled = true;
			}
		}

		protected override void UpdateDataValidation<T>(AvaloniaProperty<T> property, BindingValue<T> value)
		{
			if (property == ItemsProperty)
			{
				DataValidationErrors.SetError(this, value.Error);
			}
		}

		protected override void OnGotFocus(GotFocusEventArgs e)
		{
			base.OnGotFocus(e);
			FocusChanged(HasFocus());
		}

		protected override void OnLostFocus(RoutedEventArgs e)
		{
			base.OnLostFocus(e);
			FocusChanged(HasFocus());
		}

		private bool HasFocus()
		{
			IVisual? focused = FocusManager.Instance.Current;

			while (focused != null)
			{
				if (ReferenceEquals(focused, this))
				{
					return true;
				}

				// This helps deal with popups that may not be in the same
				// visual tree
				IVisual parent = focused.GetVisualParent();
				if (parent is null)
				{
					// Try the logical parent.
					if (focused is IControl element)
					{
						parent = element.Parent;
					}
				}

				focused = parent;
			}

			return false;
		}

		private void FocusChanged(bool hasFocus)
		{
			// The OnGotFocus & OnLostFocus are asynchronously called and cannot
			// reliably tell you that have the focus. All they do is let you
			// know that the focus changed sometime in the past. To determine
			// if you currently have the focus you need to do consult the
			// FocusManager (see HasFocus()).

			var wasFocused = _isFocused;
			_isFocused = hasFocus;

			if (hasFocus)
			{
				if (!wasFocused)
				{
					_autoCompleteBox?.Focus();
				}
			}

			PseudoClasses.Set(":focus", hasFocus);
			_isFocused = hasFocus;
		}

		private void OnDropDownClosed(object? sender, EventArgs e)
		{
			if (!(sender is AutoCompleteBox autoCompleteBox))
			{
				return;
			}

			var currentText = autoCompleteBox.Text ?? "";

			if (currentText.Length == 0 ||
				!(autoCompleteBox.SelectedItem is string selItem) ||
				selItem.Length == 0 ||
				currentText != selItem)
			{
				return;
			}

			AddTag(currentText.Trim());

			BackspaceLogicClear();

			autoCompleteBox.ClearValue(AutoCompleteBox.SelectedItemProperty);
			Dispatcher.UIThread.Post(() => { autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty); });
		}

		private void BackspaceLogicClear()
		{
			_backspaceEmptyField2 = _backspaceEmptyField1 = true;
		}

		private void OnTextChanged(object? sender, EventArgs e)
		{
			if (!(sender is AutoCompleteBox autoCompleteBox))
			{
				return;
			}

			var currentText = autoCompleteBox.Text ?? "";
			var currentTextTrimmed = currentText.Trim();

			if (RestrictInputToSuggestions && Suggestions is IList<string> suggestions)
			{
				var keywordIsInSuggestions = suggestions.Any(x => x.Equals(currentTextTrimmed,
					StringComparison.InvariantCultureIgnoreCase));

				if (!keywordIsInSuggestions)
				{
					return;
				}
			}

			if (!_isInputEnabled ||
				currentText.Length < 1 ||
				string.IsNullOrEmpty(currentTextTrimmed) ||
				!currentText.EndsWith(' '))
			{
				return;
			}

			AddTag(currentTextTrimmed);

			BackspaceLogicClear();

			Dispatcher.UIThread.Post(() => { autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty); });
		}

		private void OnKeyUp(object? sender, KeyEventArgs e)
		{
			var autoCompleteBox = sender as AutoCompleteBox;
			if (autoCompleteBox is null)
			{
				return;
			}

			var str = autoCompleteBox.Text ?? "";

			_backspaceEmptyField2 = _backspaceEmptyField1;
			_backspaceEmptyField1 = str.Length == 0;

			var strTrimmed = str.Trim();

			switch (e.Key)
			{
				case Key.Back when _backspaceEmptyField1 && _backspaceEmptyField2:
					RemoveTag();
					break;
				case Key.Enter when _isInputEnabled && !string.IsNullOrEmpty(strTrimmed):
					if (RestrictInputToSuggestions &&
						Suggestions is { } &&
						!Suggestions.Cast<string>().Any(x =>
							x.Equals(strTrimmed, StringComparison.InvariantCultureIgnoreCase)))
					{
						break;
					}

					BackspaceLogicClear();
					AddTag(strTrimmed);
					Dispatcher.UIThread.Post(() => { autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty); });
					break;
			}
		}

		private void RemoveTag()
		{
			if (Items is IList x && x.Count > 0)
			{
				x.RemoveAt(Math.Max(0, x.Count - 1));
			}

			CheckIsInputEnabled();
		}

		private void AddTag(string strTrimmed)
		{
			SelectedTag = strTrimmed;
			CheckIsInputEnabled();
		}
	}
}