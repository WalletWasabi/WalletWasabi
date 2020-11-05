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

namespace WalletWasabi.Fluent.Controls
{
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

        private CompositeDisposable? _compositeDisposable;

        private AutoCompleteBox? _autoCompleteBox;

        private bool _backspaceEmptyField1;
        private bool _backspaceEmptyField2;
        private bool _isFocused;
        private bool _isInputEnabled = true;
        private IEnumerable? _suggestions;

        static TagsBox()
        {
            ItemsProperty.OverrideMetadata<TagsBox>(
                new DirectPropertyMetadata<IEnumerable?>(enableDataValidation: true));
        }

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

        public IEnumerable? Suggestions
        {
            get => _suggestions;
            set => SetAndRaise(SuggestionsProperty, ref _suggestions, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            Presenter.ApplyTemplate();

            _compositeDisposable?.Dispose();

            _compositeDisposable = new CompositeDisposable();

            _autoCompleteBox = (Presenter.Panel as ConcatenatingWrapPanel)?.ConcatenatedChildren
                .OfType<AutoCompleteBox>().FirstOrDefault();

            if (_autoCompleteBox is null)
            {
                return;
            }

            _autoCompleteBox.KeyUp += OnKeyUp;
            _autoCompleteBox.TextChanged += OnTextChanged;
            _autoCompleteBox.DropDownClosed += OnDropDownClosed;

            Disposable.Create(
	            () =>
	            {
		            _autoCompleteBox.KeyUp -= OnKeyUp;
		            _autoCompleteBox.TextChanged -= OnTextChanged;
		            _autoCompleteBox.DropDownClosed -= OnDropDownClosed;
	            })
	            .DisposeWith(_compositeDisposable);

            _autoCompleteBox
                .AddDisposableHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel)
                .DisposeWith(_compositeDisposable);

            if (_isFocused)
            {
	            Dispatcher.UIThread.Post(() => _autoCompleteBox.Focus());
            }
        }

        private void CheckIsInputEnabled()
        {
            if (Items is IList x &&
                ItemCountLimit > 0)
            {
                _isInputEnabled = x.Count < ItemCountLimit;
            }
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
	        if (_autoCompleteBox is {})
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
            if (!(sender is AutoCompleteBox autoCompleteBox))
            {
                return;
            }

            var currentText = (autoCompleteBox.Text ?? "").Trim();

            if (currentText.Length == 0 ||
                !(autoCompleteBox.SelectedItem is string selItem) ||
                selItem.Length == 0 ||
                currentText != selItem)
            {
                return;
            }

            SelectTag(currentText);
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
            if (!(sender is AutoCompleteBox autoCompleteBox))
            {
	            return;
            }

            var currentText = autoCompleteBox.Text ?? "";
            var endsWithSpace = currentText.EndsWith(' ');
            currentText = currentText.Trim();

            if (RestrictInputToSuggestions && Suggestions is IList<string> suggestions)
            {
                var keywordIsInSuggestions =
	                suggestions.Any(
		                x => x.Equals(currentText, StringComparison.InvariantCultureIgnoreCase));

                if (!keywordIsInSuggestions)
                {
                    return;
                }
            }

            if (!_isInputEnabled ||
                currentText.Length < 1 ||
                string.IsNullOrEmpty(currentText) ||
                !endsWithSpace)
            {
                return;
            }

            SelectTag(currentText);

            BackspaceLogicClear();

            Dispatcher.UIThread.Post(() => autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty));
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (!(sender is AutoCompleteBox autoCompleteBox))
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
                    RemoveTag();
                    break;
                case Key.Enter when _isInputEnabled && !string.IsNullOrEmpty(currentText):
                    if (RestrictInputToSuggestions &&
                        Suggestions is { } &&
                        !Suggestions.Cast<string>().Any(
	                        x => x.Equals(currentText, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        break;
                    }

                    BackspaceLogicClear();
                    SelectTag(currentText);
                    Dispatcher.UIThread.Post(() => autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty));
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

        private void SelectTag(string tagString)
        {
            SelectedTag = tagString;
            CheckIsInputEnabled();
        }
    }
}