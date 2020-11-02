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

        private readonly CompositeDisposable _compositeDisposable = new CompositeDisposable();

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

            _autoCompleteBox = (Presenter.Panel as ConcatenatingWrapPanel)?.ConcatenatedChildren
                .OfType<AutoCompleteBox>().FirstOrDefault();

            if (_autoCompleteBox is null)
            {
                return;
            }

            _autoCompleteBox.KeyUp += OnKeyUp;
            _autoCompleteBox.TextChanged += OnTextChanged;
            _autoCompleteBox.DropDownClosed += OnDropDownClosed;

            _autoCompleteBox
                .AddDisposableHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel)
                .DisposeWith(_compositeDisposable);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _compositeDisposable.Dispose();
            base.OnDetachedFromVisualTree(e);
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
                if (ReferenceEquals(focused, this)) return true;

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
            // reliably tell you that have the focus.  All they do is let you
            // know that the focus changed sometime in the past.  To determine
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

            _isFocused = hasFocus;
        }

        private void OnDropDownClosed(object? sender, EventArgs e)
        {
            if (!(sender is AutoCompleteBox autoCompleteBox))
            {
                return;
            }

            var currentText = autoCompleteBox.Text.Trim();

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
            
            Dispatcher.UIThread.Post(() => { autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty); });
        }

        private void BackspaceLogicClear()
        {
            _backspaceEmptyField2 = _backspaceEmptyField1 = true;
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            if (!(sender is AutoCompleteBox autoCompleteBox)) return;

            var currentText = autoCompleteBox.Text ?? "";
            var endsWithSpace = currentText.EndsWith(' ');
            currentText = currentText.Trim();

            if (RestrictInputToSuggestions && Suggestions is IList<string> suggestions)
            {
                var keywordIsInSuggestions = suggestions.Any(x => x.Equals(currentText,
                    StringComparison.InvariantCultureIgnoreCase));

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

            Dispatcher.UIThread.Post(() => { autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty); });
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            var autoCompleteBox = sender as AutoCompleteBox;
            if (autoCompleteBox is null) return;

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
                    SelectTag(strTrimmed);
                    Dispatcher.UIThread.Post(() => { autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty); });
                    break;
            }
        }

        private void RemoveTag()
        {
            if (Items is IList x && x.Count > 0) x.RemoveAt(Math.Max(0, x.Count - 1));
            CheckIsInputEnabled();
        }

        private void SelectTag(string strTrimmed)
        {
            SelectedTag = strTrimmed;
            CheckIsInputEnabled();
        }
    }
}