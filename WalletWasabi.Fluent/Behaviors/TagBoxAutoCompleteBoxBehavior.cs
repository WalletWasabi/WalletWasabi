using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
    public class TagBoxAutoCompleteBoxBehavior : Behavior<AutoCompleteBox>
    {
        public static readonly StyledProperty<Action> CommitTextActionProperty =
            AvaloniaProperty.Register<SplitViewAutoBehavior, Action>(nameof(CommitTextAction));

        public static readonly StyledProperty<Action> BackspaceAndEmptyTextActionProperty =
            AvaloniaProperty.Register<SplitViewAutoBehavior, Action>(nameof(BackspaceAndEmptyTextAction));

        public Action CommitTextAction
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
            AssociatedObject.KeyUp += OnKeyUp;
            AssociatedObject.TextChanged += OnTextChanged;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Refocus because the old control is destroyed
                // when the tag list changes.
                AssociatedObject.Focus();
            });


            base.OnAttached();
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            var obj = AssociatedObject?.Text ?? "";

            if (obj.Length > 1 && !string.IsNullOrEmpty(obj.Trim()) && obj.EndsWith(' '))
            {
                CommitTextAction?.Invoke(); 
            }
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && AssociatedObject?.Text.Length == 0)
            {
                BackspaceAndEmptyTextAction?.Invoke();
            }
            else if (e.Key == Key.Enter && !string.IsNullOrEmpty(AssociatedObject?.Text.Trim() ?? ""))
            {
                CommitTextAction?.Invoke();
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.TextChanged -= OnTextChanged;
            AssociatedObject.KeyUp -= OnKeyUp;
        }
    }
}