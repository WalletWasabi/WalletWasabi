using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;

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

        public static readonly DirectProperty<TagsBox, IEnumerable> SuggestionsProperty =
            AvaloniaProperty.RegisterDirect<TagsBox, IEnumerable>(
                nameof(Suggestions),
                o => o.Suggestions,
                (o, v) => o.Suggestions = v);

        private AutoCompleteBox? _autoCompleteBox;

        private bool _backspaceEmptyField1;
        private bool _backspaceEmptyField2;


        private IDisposable? _disposable;

        private bool _isInputEnabled = true;
        private IEnumerable _suggestionsEnumerable;

        public TagsBox()
        {
            this.WhenAnyValue(x => x.Items)
                .Subscribe(RegisterIsInputEnabledListener);
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

        public IEnumerable Suggestions
        {
            get => _suggestionsEnumerable;
            set => SetAndRaise(SuggestionsProperty, ref _suggestionsEnumerable, value);
        }

        private void RegisterIsInputEnabledListener(IEnumerable enumerable)
        {
            if (Items is null || ItemCountLimit == 0) return;

            if (Items is IList x)
                _isInputEnabled = x.Count < ItemCountLimit;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            Presenter.ApplyTemplate();

            _autoCompleteBox = (Presenter.Panel as ConcatenatingWrapPanel)?.ConcatenatedChildren
                .OfType<AutoCompleteBox>().FirstOrDefault();

            if (_autoCompleteBox is null) return;

            _autoCompleteBox.KeyUp += OnKeyUp;
            _autoCompleteBox.TextChanged += OnTextChanged;
            _autoCompleteBox.DropDownClosed += OnDropDownClosed;

            _disposable =
                _autoCompleteBox.AddDisposableHandler(TextInputEvent, OnTextInput,
                    RoutingStrategies.Tunnel);

            _autoCompleteBox.Focus();
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (_autoCompleteBox is null) return;

            if (!_isInputEnabled)
            {
                e.Handled = true;
                return;
            }

            if (RestrictInputToSuggestions && !Suggestions.Cast<string>().Any(x =>
                x.StartsWith(_autoCompleteBox.SearchText ?? "", true, CultureInfo.CurrentCulture)))
                e.Handled = true;
        }

        protected override void UpdateDataValidation<T>(AvaloniaProperty<T> property, BindingValue<T> value)
        {
            if (property == ItemsProperty) 
                DataValidationErrors.SetError(this, value.Error);
        }

        private void OnDropDownClosed(object? sender, EventArgs e)
        {
            if (_autoCompleteBox is null) return;

            var currentText = _autoCompleteBox.Text ?? "";

            if (currentText.Length == 0 || !(_autoCompleteBox.SelectedItem is string selItem) || selItem.Length == 0 ||
                currentText != selItem)
                return;

            AddTag(currentText.Trim());
            
            BackspaceLogicClear();
            
            _autoCompleteBox.SelectedItem = null;
            _autoCompleteBox.Text = string.Empty;
        }

        private void BackspaceLogicClear()
        {
            _backspaceEmptyField2 = _backspaceEmptyField1 = false;
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            if (_autoCompleteBox is null) return;

            var currentText = _autoCompleteBox.Text ?? "";
            var currentTextTrimmed = currentText.Trim();

            if (!_isInputEnabled ||
                currentText.Length < 1 ||
                string.IsNullOrEmpty(currentTextTrimmed) ||
                !currentText.EndsWith(' ') ||
                RestrictInputToSuggestions && !Suggestions.Cast<string>().Any(x => x.Equals(currentTextTrimmed,
                    StringComparison.InvariantCultureIgnoreCase)))
                return;

            AddTag(currentTextTrimmed);

            BackspaceLogicClear();

            Dispatcher.UIThread.Post(() => { _autoCompleteBox?.ClearValue(AutoCompleteBox.TextProperty); });
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (_autoCompleteBox is null) return;

            var str = _autoCompleteBox?.Text ?? "";

            _backspaceEmptyField2 = _backspaceEmptyField1;
            _backspaceEmptyField1 = str.Length == 0;

            var strTrimmed = str.Trim();

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (e.Key)
            {
                case Key.Back when _backspaceEmptyField1 && _backspaceEmptyField2:
                    RemoveTag();
                    break;
                case Key.Enter when _isInputEnabled && !string.IsNullOrEmpty(strTrimmed):
                    if (RestrictInputToSuggestions && !Suggestions.Cast<string>().Any(x =>
                        x.Equals(strTrimmed, StringComparison.InvariantCultureIgnoreCase)))
                        break;

                    BackspaceLogicClear();

                    AddTag(strTrimmed);
                    if (_autoCompleteBox is null) break;
                    _autoCompleteBox.Text = string.Empty;
                    break;
            }
        }

        private void RemoveTag()
        {
            if (Items is IList x) x.RemoveAt(Math.Max(0, x.Count - 1));
        }

        private void AddTag(string strTrimmed)
        {
            if (Items is IList x) x.Add(strTrimmed);
        }
    }
}