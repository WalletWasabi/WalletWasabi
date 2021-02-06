using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace WalletWasabi.Fluent.Controls
{
	public class TagsBox : TemplatedControl
	{
		public static readonly StyledProperty<bool> RestrictInputToSuggestionsProperty =
			AvaloniaProperty.Register<TagsBox, bool>(nameof(RestrictInputToSuggestions));

		public static readonly StyledProperty<int> ItemCountLimitProperty =
			AvaloniaProperty.Register<TagsBox, int>(nameof(ItemCountLimit));

		public static readonly StyledProperty<char> TagSeparatorProperty =
			AvaloniaProperty.Register<TagsBox, char>(nameof(TagSeparator), defaultValue: ' ');

		public static readonly DirectProperty<TagsBox, IEnumerable<string>> ItemsProperty =
			AvaloniaProperty.RegisterDirect<TagsBox, IEnumerable<string>>(nameof(Items),
				o => o.Items,
				(o, v) => o.Items = v,
				enableDataValidation: true);

		public static readonly DirectProperty<TagsBox, IEnumerable?> SuggestionsProperty =
			AvaloniaProperty.RegisterDirect<TagsBox, IEnumerable?>(
				nameof(Suggestions),
				o => o.Suggestions,
				(o, v) => o.Suggestions = v);

		private CompositeDisposable? _compositeDisposable;
		private AutoCompleteBox? _autoCompleteBox;
		private bool _backspaceEmptyField1;
		private bool _backspaceEmptyField2;
		private bool _isFocused;
		private bool _isInputEnabled = true;
		private IEnumerable? _suggestions;
		private ICommand? _completedCommand;
		private IEnumerable<string> _items;

		public static readonly DirectProperty<TagsBox, ICommand?> CompletedCommandProperty =
			AvaloniaProperty.RegisterDirect<TagsBox, ICommand?>(
				nameof(CompletedCommand),
				o => o.CompletedCommand,
				(o, v) => o.CompletedCommand = v);

		public static readonly StyledProperty<bool> IsReadOnlyProperty =
			AvaloniaProperty.Register<TagsBox, bool>("IsReadOnly");

		[Content]
		public IEnumerable<string> Items
		{
			get => _items;
			set => SetAndRaise(ItemsProperty, ref _items, value);
		}

		public bool RestrictInputToSuggestions
		{
			get => GetValue(RestrictInputToSuggestionsProperty);
			set => SetValue(RestrictInputToSuggestionsProperty, value);
		}

		public int ItemCountLimit
		{
			get => GetValue(ItemCountLimitProperty);
			set => SetValue(ItemCountLimitProperty, value);
		}

		public char TagSeparator
		{
			get => GetValue(TagSeparatorProperty);
			set => SetValue(TagSeparatorProperty, value);
		}

		public IEnumerable? Suggestions
		{
			get => _suggestions;
			set => SetAndRaise(SuggestionsProperty, ref _suggestions, value);
		}

		public ICommand? CompletedCommand
		{
			get => _completedCommand;
			set => SetAndRaise(CompletedCommandProperty, ref _completedCommand, value);
		}

		public bool IsReadOnly
		{
			get => GetValue(IsReadOnlyProperty);
			set => SetValue(IsReadOnlyProperty, value);
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_compositeDisposable?.Dispose();

			_compositeDisposable = new CompositeDisposable();

			var Presenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");

			Presenter.ApplyTemplate();

			_autoCompleteBox = (Presenter.Panel as ConcatenatingWrapPanel)?.ConcatenatedChildren
				.OfType<AutoCompleteBox>().FirstOrDefault();

			if (_autoCompleteBox is null)
			{
				return;
			}

			_autoCompleteBox.TextChanged += OnTextChanged;
			_autoCompleteBox.DropDownClosed += OnDropDownClosed;

			Disposable.Create(
					() =>
					{
						_autoCompleteBox.TextChanged -= OnTextChanged;
						_autoCompleteBox.DropDownClosed -= OnDropDownClosed;
					})
				.DisposeWith(_compositeDisposable);

			_autoCompleteBox
				.AddDisposableHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel)
				.DisposeWith(_compositeDisposable);

			_autoCompleteBox
				.AddDisposableHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel)
				.DisposeWith(_compositeDisposable);

			if (_isFocused)
			{
				Dispatcher.UIThread.Post(() => _autoCompleteBox.Focus());
			}
		}

		private void CheckIsInputEnabled()
		{
			if (Items is IList x && ItemCountLimit > 0)
			{
				_isInputEnabled = x.Count < ItemCountLimit;
			}
		}

		private void OnTextInput(object? sender, TextInputEventArgs e)
		{
			if (sender is not AutoCompleteBox autoCompleteBox)
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
				!suggestions.Any(x => x.StartsWith(autoCompleteBox.SearchText ?? "", true, CultureInfo.CurrentCulture)))
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
			FocusChanged();
		}

		protected override void OnLostFocus(RoutedEventArgs e)
		{
			base.OnLostFocus(e);
			FocusChanged();
		}

		private void FocusChanged()
		{
			if (_autoCompleteBox is { })
			{
				if (IsKeyboardFocusWithin && !_autoCompleteBox.IsKeyboardFocusWithin)
				{
					_autoCompleteBox?.Focus();
				}
			}

			_isFocused = IsKeyboardFocusWithin;
		}

		private void OnDropDownClosed(object? sender, EventArgs e)
		{
			if (sender is not AutoCompleteBox autoCompleteBox)
			{
				return;
			}

			var currentText = (autoCompleteBox.Text ?? "").Trim();

			if (currentText.Length == 0 ||
			    autoCompleteBox.SelectedItem is not string selItem ||
			    selItem.Length == 0 ||
			    currentText != selItem)
			{
				return;
			}

			AddTag(currentText);
			BackspaceLogicClear();
			autoCompleteBox.ClearValue(AutoCompleteBox.SelectedItemProperty);

			Dispatcher.UIThread.Post(() => autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty));
		}

		private void BackspaceLogicClear()
		{
			_backspaceEmptyField2 = _backspaceEmptyField1 = true;
		}

		private void OnTextChanged(object? sender, EventArgs e)
		{
			if (sender is not AutoCompleteBox autoCompleteBox)
			{
				return;
			}

			var currentText = autoCompleteBox.Text ?? "";
			var endsWithSeparator = currentText.EndsWith(TagSeparator);
			currentText = currentText.Trim();

			var splitTags = currentText.Split(TagSeparator);
			if (splitTags.Length == 1 && Suggestions is IList<string> suggestions)
			{
				if (RestrictInputToSuggestions)
				{
					var keywordIsInSuggestions =
						suggestions.Any(
							x => x.Equals(currentText, StringComparison.InvariantCultureIgnoreCase));

					if (!keywordIsInSuggestions)
					{
						return;
					}
				}
				else if (endsWithSeparator)
				{
					foreach (var tag in splitTags)
					{
						if (!RestrictInputToSuggestions)
						{
							AddTag(tag);
							continue;
						}

						var keywordIsInSuggestions =
							suggestions.Any(
								x => x.Equals(tag, StringComparison.InvariantCultureIgnoreCase));

						if (keywordIsInSuggestions)
						{
							AddTag(tag);
						}
					}

					Dispatcher.UIThread.Post(() => autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty));
					return;
				}
			}

			if (!_isInputEnabled ||
				currentText.Length < 1 ||
				string.IsNullOrEmpty(currentText) ||
				!endsWithSeparator)
			{
				return;
			}

			AddTag(currentText);

			BackspaceLogicClear();

			Dispatcher.UIThread.Post(() => autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty));
		}

		private void OnKeyUp(object? sender, KeyEventArgs e)
		{
			if (sender is not AutoCompleteBox autoCompleteBox)
			{
				return;
			}

			var currentText = autoCompleteBox.Text ?? "";

			_backspaceEmptyField2 = _backspaceEmptyField1;
			_backspaceEmptyField1 = currentText.Length == 0;

			currentText = currentText.Trim();

			switch (e.Key)
			{
				case Key.Back when _backspaceEmptyField1 && _backspaceEmptyField2:
					RemoveLastTag();
					break;
				case Key.Enter when _isInputEnabled && !string.IsNullOrEmpty(currentText):
					// Reject entry of the tag when user pressed enter and
					// the input tag is not on the suggestions list.
					if (RestrictInputToSuggestions && Suggestions is { } &&
					    !Suggestions.Cast<string>().Any(
						    x => x.Equals(currentText, StringComparison.InvariantCultureIgnoreCase)))
					{

						break;
					}

					BackspaceLogicClear();
					AddTag(currentText);
					ExecuteCompletedCommand();

					Dispatcher.UIThread.Post(() => autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty));
					break;

				case Key.Enter:
					ExecuteCompletedCommand();
					break;
			}
		}

		private void ExecuteCompletedCommand()
		{
			if (Items is IList x && x.Count >= ItemCountLimit)
			{
				if (CompletedCommand is { } && CompletedCommand.CanExecute(null))
				{
					CompletedCommand.Execute(null);
				}
			}
		}

		private void RemoveLastTag()
		{
			if (Items is IList x && x.Count > 0)
			{
				x.RemoveAt(Math.Max(0, x.Count - 1));
			}

			CheckIsInputEnabled();
		}

		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> e)
		{
			base.OnPropertyChanged(e);

			if (e.Property == IsReadOnlyProperty)
			{
				PseudoClasses.Set(":readonly", IsReadOnly);
			}
		}

		internal void RemoveTargetTag(object? tag)
		{
			if (Items is IList {Count: > 0} x && tag is { })
			{
				x.RemoveAt(x.IndexOf(tag));
			}

			CheckIsInputEnabled();
		}

		private void AddTag(string tag)
		{
			if (Items is IList x && x.Count + 1 <= ItemCountLimit)
			{
				x.Add(tag);
			}

			CheckIsInputEnabled();
		}
	}
}