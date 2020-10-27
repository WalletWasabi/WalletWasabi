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

namespace WalletWasabi.Fluent.Controls
{
    public class TagsBoxAutoCompleteBoxBehavior : Behavior<AutoCompleteBox>
    {
        public static readonly StyledProperty<bool> RestrictInputToSuggestionsProperty =
            AvaloniaProperty.Register<TagsBoxAutoCompleteBoxBehavior, bool>(nameof(RestrictInputToSuggestions));

        public static readonly StyledProperty<bool> IsInputEnabledProperty =
            AvaloniaProperty.Register<TagsBoxAutoCompleteBoxBehavior, bool>(nameof(IsInputEnabled));

        public static readonly StyledProperty<IEnumerable<string>> SuggestionsProperty =
            AvaloniaProperty.Register<TagsBoxAutoCompleteBoxBehavior, IEnumerable<string>>(nameof(Suggestions));

        public static readonly StyledProperty<Action<string>> CommitTextActionProperty =
            AvaloniaProperty.Register<TagsBoxAutoCompleteBoxBehavior, Action<string>>(nameof(CommitTextAction));

        public static readonly StyledProperty<Action> BackspaceAndEmptyTextActionProperty =
            AvaloniaProperty.Register<TagsBoxAutoCompleteBoxBehavior, Action>(nameof(BackspaceAndEmptyTextAction));

        public static readonly StyledProperty<Action> GrabFocusActionProperty =
            AvaloniaProperty.Register<TagsBoxAutoCompleteBoxBehavior, Action>(nameof(GrabFocusActionProperty));

        private IDisposable? _disposable;

        private bool _bs1;
        private bool _bs2;

        public bool RestrictInputToSuggestions
        {
            get => GetValue(RestrictInputToSuggestionsProperty);
            set => SetValue(RestrictInputToSuggestionsProperty, value);
        }


        public bool IsInputEnabled
        {
            get => GetValue(IsInputEnabledProperty);
            set => SetValue(IsInputEnabledProperty, value);
        }

        public IEnumerable<string> Suggestions
        {
            get => GetValue(SuggestionsProperty);
            set => SetValue(SuggestionsProperty, value);
        }

        public Action<string> CommitTextAction
        {
            get => GetValue(CommitTextActionProperty);
            set => SetValue(CommitTextActionProperty, value);
        }

        public Action BackspaceAndEmptyTextAction
        {
            get => GetValue(BackspaceAndEmptyTextActionProperty);
            set => SetValue(BackspaceAndEmptyTextActionProperty, value);
        }


        public Action GrabFocusAction
        {
            get => GetValue(GrabFocusActionProperty);
            set => SetValue(GrabFocusActionProperty, value);
        }

        protected override void OnAttached()
        {
            if (AssociatedObject is null) return;

            AssociatedObject.KeyUp += OnKeyUp;
            AssociatedObject.TextChanged += OnTextChanged;
            AssociatedObject.DropDownClosed += OnDropDownClosed;
            GrabFocusAction += DoGrabFocus;
            _disposable =
                AssociatedObject.AddDisposableHandler(InputElement.TextInputEvent, OnTextInput,
                    RoutingStrategies.Tunnel);

            // Refocus because the old control is destroyed
            // when the tag list changes.
            DoGrabFocus();

            base.OnAttached();
        }

        private void DoGrabFocus()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!AssociatedObject?.IsFocused ?? false) AssociatedObject?.Focus();
            });
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (AssociatedObject is null) return;

            if (!IsInputEnabled)
            {
                e.Handled = true;
                return;
            }

            if (RestrictInputToSuggestions && !Suggestions.Any(x =>
                x.StartsWith(AssociatedObject.SearchText ?? "", true, CultureInfo.CurrentCulture)))
                e.Handled = true;
        }

        private void OnDropDownClosed(object? sender, EventArgs e)
        {
            if (AssociatedObject is null) return;

            var currentText = AssociatedObject.Text ?? "";

            if (currentText.Length == 0 || !(AssociatedObject.SelectedItem is string selItem) || selItem.Length == 0 ||
                currentText != selItem)
            {
                return;
            }

            CommitTextAction.Invoke(currentText.Trim());
            AssociatedObject.ClearValue(AutoCompleteBox.SelectedItemProperty);

            BackspaceLogicClear();

            Dispatcher.UIThread.Post(() => { AssociatedObject.ClearValue(AutoCompleteBox.TextProperty); });
        }

        private void BackspaceLogicClear()
        {
            _bs1 = false;
            _bs2 = false;
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            if (AssociatedObject is null) return;

            var currentText = AssociatedObject.Text ?? "";
            var currentTextTrimmed = currentText.Trim();

            if (!IsInputEnabled ||
                currentText.Length < 1 ||
                string.IsNullOrEmpty(currentTextTrimmed) ||
                !currentText.EndsWith(' ') ||
                (RestrictInputToSuggestions && !Suggestions.Any(x => x.Equals(currentTextTrimmed,
                    StringComparison.InvariantCultureIgnoreCase))))
                return;

            CommitTextAction?.Invoke(currentTextTrimmed);

            BackspaceLogicClear();

            Dispatcher.UIThread.Post(() => { AssociatedObject?.ClearValue(AutoCompleteBox.TextProperty); });
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (AssociatedObject is null) return;

            var str = AssociatedObject?.Text ?? "";

            _bs2 = _bs1;
            _bs1 = str.Length == 0;

            var strTrimmed = str.Trim();

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (e.Key)
            {
                case Key.Back when _bs1 && _bs2:
                    BackspaceAndEmptyTextAction?.Invoke();
                    break;
                case Key.Enter when IsInputEnabled && !string.IsNullOrEmpty(strTrimmed) :
                    if (RestrictInputToSuggestions && !Suggestions.Any(x =>
                        x.Equals(strTrimmed, StringComparison.InvariantCultureIgnoreCase)))
                        break;

                    BackspaceLogicClear();

                    CommitTextAction?.Invoke(strTrimmed);
                    Dispatcher.UIThread.Post(() => { AssociatedObject?.ClearValue(AutoCompleteBox.TextProperty); });
                    break;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject is null) return;

            base.OnDetaching();

            AssociatedObject.DropDownClosed -= OnDropDownClosed;
            AssociatedObject.KeyUp -= OnKeyUp;
            AssociatedObject.TextChanged -= OnTextChanged;
            GrabFocusAction -= DoGrabFocus;

            _disposable?.Dispose();

            BackspaceLogicClear();
        }
    }
}