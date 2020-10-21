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
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors
{
    public class TagBoxAutoCompleteBoxBehavior : Behavior<AutoCompleteBox>
    {
        public static readonly StyledProperty<IEnumerable<string>> SuggestionsProperty =
            AvaloniaProperty.Register<SplitViewAutoBehavior, IEnumerable<string>>(nameof(Suggestions));

        public static readonly StyledProperty<Action<string>> CommitTextActionProperty =
            AvaloniaProperty.Register<SplitViewAutoBehavior, Action<string>>(nameof(CommitTextAction));

        public static readonly StyledProperty<Action> BackspaceAndEmptyTextActionProperty =
            AvaloniaProperty.Register<SplitViewAutoBehavior, Action>(nameof(BackspaceAndEmptyTextAction));

        private IDisposable _disposable;

        private readonly DropOutStack<string> op = new DropOutStack<string>(2);

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

        protected override void OnAttached()
        {
            if (AssociatedObject is null) return;

            AssociatedObject.KeyUp += OnKeyUp;
            AssociatedObject.TextChanged += OnTextChanged;
            AssociatedObject.DropDownClosed += OnDropDownClosed;
            _disposable =
                AssociatedObject.AddDisposableHandler(InputElement.TextInputEvent, OnTextInput,
                    RoutingStrategies.Tunnel);


            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Refocus because the old control is destroyed
                // when the tag list changes.
                AssociatedObject.Focus();
            });

            base.OnAttached();
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (AssociatedObject is null)
            {
                return;
            }

            if (!Suggestions.Any(x =>
                x.StartsWith(AssociatedObject.SearchText ?? "", true, CultureInfo.CurrentCulture)))
            {
                e.Handled = true;
            }
        }

        private void OnDropDownClosed(object? sender, EventArgs e)
        {
            if (AssociatedObject is null)
            {
                return;
            }

            var currentText = AssociatedObject.Text ?? "";
            var selItem = AssociatedObject.SelectedItem as string;

            if (currentText.Length == 0) return;

            if (selItem is null || selItem.Length == 0 || currentText != selItem) return;

            CommitTextAction?.Invoke(currentText.Trim());
            AssociatedObject.ClearValue(AutoCompleteBox.SelectedItemProperty);

            Dispatcher.UIThread.Post(() => { AssociatedObject.ClearValue(AutoCompleteBox.TextProperty); });
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            if (AssociatedObject is null)
            {
                return;
            }

            var currentText = AssociatedObject.Text ?? "";

            var currentTextTrimmed = currentText.Trim();

            if (currentText.Length < 1 || string.IsNullOrEmpty(currentTextTrimmed) || !currentText.EndsWith(' ') ||
                !Suggestions.Any(x => x.Equals(currentTextTrimmed,
                    StringComparison.InvariantCultureIgnoreCase)))
                return;

            CommitTextAction?.Invoke(currentTextTrimmed);
            Dispatcher.UIThread.Post(() => { AssociatedObject?.ClearValue(AutoCompleteBox.TextProperty); });
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (AssociatedObject is null) return;

            var str = AssociatedObject?.Text ?? "";

            op.Push(str);

            var strTrimmed = str.Trim();

            var p1 = op.Items.Length >= 2 ? op.Items[0] : "";
            var p2 = op.Items.Length >= 2 ? op.Items[1] : "";

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (e.Key)
            {
                case Key.Back when string.IsNullOrEmpty(p1) && string.IsNullOrEmpty(p2):
                    BackspaceAndEmptyTextAction?.Invoke();
                    break;
                case Key.Enter when !string.IsNullOrEmpty(strTrimmed):
                    if (!Suggestions.Any(x => x.Equals(strTrimmed, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        break;
                    }
                    op.Clear();
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

            _disposable?.Dispose();
        }
    }
}