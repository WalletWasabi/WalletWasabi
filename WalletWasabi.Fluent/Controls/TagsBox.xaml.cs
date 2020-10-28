using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.TagsBox;
using System;
using System.Collections;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

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

        private bool _bs1;
        private bool _bs2;


        private IDisposable? _disposable;

        private bool _isInputEnabled = true;
        private IEnumerable _suggestionsEnumerable;

        public TagsBox()
        {
            this.WhenAnyValue(x => x.Items)
                .Subscribe(RegisterIsInputEnabledListener);
  
            
        }

        private void RegisterIsInputEnabledListener(IEnumerable enumerable)
        { 
            if(Items is null || ItemCountLimit == 0) return;

            _isInputEnabled = Items.Cast<object>().Count() < ItemCountLimit;
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

            _autoCompleteBox?.Focus();
            
            
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

        private void OnDropDownClosed(object? sender, EventArgs e)
        {
            if (_autoCompleteBox is null) return;

            var currentText = _autoCompleteBox.Text ?? "";

            if (currentText.Length == 0 || !(_autoCompleteBox.SelectedItem is string selItem) || selItem.Length == 0 ||
                currentText != selItem)
                return;

            AddTag(currentText.Trim());
            _autoCompleteBox.ClearValue(AutoCompleteBox.SelectedItemProperty);

            BackspaceLogicClear();

            Dispatcher.UIThread.Post(() => { _autoCompleteBox.ClearValue(AutoCompleteBox.TextProperty); });
        }

        private void BackspaceLogicClear()
        {
            _bs1 = false;
            _bs2 = false;
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

            _bs2 = _bs1;
            _bs1 = str.Length == 0;

            var strTrimmed = str.Trim();

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (e.Key)
            {
                case Key.Back when _bs1 && _bs2:
                    RemoveTag();
                    break;
                case Key.Enter when _isInputEnabled && !string.IsNullOrEmpty(strTrimmed):
                    if (RestrictInputToSuggestions && !Suggestions.Cast<string>().Any(x =>
                        x.Equals(strTrimmed, StringComparison.InvariantCultureIgnoreCase)))
                        break;

                    BackspaceLogicClear();

                    AddTag(strTrimmed);
                    Dispatcher.UIThread.Post(() => { _autoCompleteBox?.ClearValue(AutoCompleteBox.TextProperty); });
                    break;
            }
        }

        private void RemoveTag()
        {
            var total = Items.Cast<object>().Count();
            var targetIndex = Math.Max(0, total - 1);
            Items = Items.Cast<object>().Where((car, index) => index != targetIndex);
        }

        private void AddTag(string strTrimmed)
        {
            Items = Items.Cast<object>()
                .Concat(new[] {new TagViewModel((TagsBoxViewModel) DataContext, strTrimmed)});
        }

        // protected override void OnDetaching()
        // {
        //     if (_autoCompleteBox is null) return;
        //
        //     base.OnDetaching();
        //
        //     _autoCompleteBox.DropDownClosed -= OnDropDownClosed;
        //     _autoCompleteBox.KeyUp -= OnKeyUp;
        //     _autoCompleteBox.TextChanged -= OnTextChanged;
        //     GrabFocusAction -= DoGrabFocus;
        //
        //     _disposable?.Dispose();
        //
        //     BackspaceLogicClear();
        // }
    }
}