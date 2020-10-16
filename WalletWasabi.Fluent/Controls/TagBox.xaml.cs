using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace WalletWasabi.Fluent.Controls
{
    /// <summary>
    /// </summary>
    public class TagBox : ContentControl
    {
        public static readonly StyledProperty<bool> IsDialogOpenProperty =
            AvaloniaProperty.Register<TagBox, bool>(nameof(IsDialogOpen));

        public bool IsDialogOpen
        {
            get => GetValue(IsDialogOpenProperty);
            set => SetValue(IsDialogOpenProperty, value);
        }

        public static readonly StyledProperty<int> MinimumPrefixLengthProperty =
            AvaloniaProperty.Register<TagBox, int>(nameof(MinimumPrefixLength));    

        public int MinimumPrefixLength
        {
            get => GetValue(MinimumPrefixLengthProperty);
            set => SetValue(MinimumPrefixLengthProperty, value);
        }

        public static readonly StyledProperty<IEnumerable> SuggestionsProperty =
            AvaloniaProperty.Register<TagBox, IEnumerable>(nameof(Suggestions));    

        public IEnumerable Suggestions
        {
            get => GetValue(SuggestionsProperty);
            set => SetValue(SuggestionsProperty, value);
        }


        private WrapPanel _wp;
        private AutoCompleteBox _tb;

        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsDialogOpenProperty)
            {
                PseudoClasses.Set(":open", change.NewValue.GetValueOrDefault<bool>());
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            _wp = e.NameScope.Find<WrapPanel>("PART_Container"); ;
            _tb = e.NameScope.Find<AutoCompleteBox>("PART_InputBox"); ;
            _tb.KeyUp += OnKeyPress;
            _tb.GetObservable<string>(TextBox.TextProperty).Subscribe(TextChanged);
        }

        private void TextChanged(string obj)
        {
            if (obj.Length > 1 && !string.IsNullOrEmpty(obj.Trim()) && obj.EndsWith(' '))
            {
                var index = _wp.Children.Count - 1;

                _wp.Children.Insert(index, new TextBlock() { Text = obj.Trim(), Margin = new Thickness(5) });

                Dispatcher.UIThread.InvokeAsync(() => { _tb.Text = string.Empty; });
            }
        }

        private void OnKeyPress(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && string.IsNullOrEmpty(_tb.Text.Trim()) && _wp.Children.Count > 1)
            {
                _wp.Children.RemoveAt(_wp.Children.Count - 2);
            }
            else if (e.Key == Key.Enter && !string.IsNullOrEmpty(_tb.Text.Trim()))
            {
                var index = _wp.Children.Count - 1;

                _wp.Children.Insert(index, new TextBlock() { Text = _tb.Text.Trim(), Margin = new Thickness(5) });

                Dispatcher.UIThread.InvokeAsync(() => { _tb.Text = string.Empty; });
            }
        }
    }
}
