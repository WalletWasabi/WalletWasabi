using System.Collections;
using System.Linq;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Fluent.ViewModels.TagsBox;

namespace WalletWasabi.Fluent.Controls
{
    /// <summary>
    /// </summary>
    public class TagsBox : ItemsControl
    {
        private IEnumerable _suggestionsEnumerable;
        private AutoCompleteBox? _autoCompleteBox;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            Presenter.ApplyTemplate();

            _autoCompleteBox = (Presenter.Panel as ConcatenatingWrapPanel)?.ConcatenatedChildren
                .OfType<AutoCompleteBox>().FirstOrDefault();


            _autoCompleteBox.KeyUp += OnKeyUp;
            _autoCompleteBox.TextChanged += OnTextChanged;
            _autoCompleteBox.DropDownClosed += OnDropDownClosed;

            _disposable =
                _autoCompleteBox.AddDisposableHandler(InputElement.TextInputEvent, OnTextInput,
                    RoutingStrategies.Tunnel);
            
            _autoCompleteBox?.Focus();
        }

        public static readonly DirectProperty<TagsBox, IEnumerable> SuggestionsProperty =
            AvaloniaProperty.RegisterDirect<TagsBox, IEnumerable>(
                nameof(Suggestions),
                o => o.Suggestions,
                (o, v) => o.Suggestions = v);

        public IEnumerable Suggestions
        {
            get => _suggestionsEnumerable;
            set => SetAndRaise(SuggestionsProperty, ref _suggestionsEnumerable, value);
        }


        private IDisposable? _disposable;

        private bool _bs1;
        private bool _bs2;

        public bool RestrictInputToSuggestions { get; set; }

        public bool IsInputEnabled { get; set; } = true;


        // public Action GrabFocusAction()
        // {
        //     if (!_autoCompleteBox?.IsFocused ?? false) _autoCompleteBox?.Focus();
        // }

        // protected override void OnAttached()
        // {
        //     // if (_autoCompleteBox is null) return;
        //     //
        //     //
        //     // // Refocus because the old control is destroyed
        //     // // when the tag list changes.
        //     // DoGrabFocus();
        //     //
        //     // base.OnAttached();
        // }
        //

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (_autoCompleteBox is null) return;

            if (!IsInputEnabled)
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
            {
                return;
            }

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

            if (!IsInputEnabled ||
                currentText.Length < 1 ||
                string.IsNullOrEmpty(currentTextTrimmed) ||
                !currentText.EndsWith(' ') ||
                (RestrictInputToSuggestions && !Suggestions.Cast<string>().Any(x => x.Equals(currentTextTrimmed,
                    StringComparison.InvariantCultureIgnoreCase))))
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
                case Key.Enter when IsInputEnabled && !string.IsNullOrEmpty(strTrimmed):
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
            Items = Items.Cast<object>().Concat(new[] {new TagViewModel((TagsBoxViewModel) this.DataContext,strTrimmed)});
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