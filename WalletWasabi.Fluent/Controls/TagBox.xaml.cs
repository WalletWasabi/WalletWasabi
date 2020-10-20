using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.TagsBox;

namespace WalletWasabi.Fluent.Controls
{
    /// <summary>
    /// </summary>
    public class TagBox : ContentControl
    {
        // public static readonly StyledProperty<int> MinimumPrefixLengthProperty =
        //     AvaloniaProperty.Register<TagBox, int>(nameof(MinimumPrefixLength));
        //
        // public static readonly StyledProperty<IEnumerable> SuggestionsProperty =
        //     AvaloniaProperty.Register<TagBox, IEnumerable>(nameof(Suggestions));
        //
        // private AutoCompleteBox _tb;
        //
        //
        // private WrapPanel _wp;
        //
        // public int MinimumPrefixLength
        // {
        //     get => GetValue(MinimumPrefixLengthProperty);
        //     set => SetValue(MinimumPrefixLengthProperty, value);
        // }
        //
        // public IEnumerable Suggestions
        // {
        //     get => GetValue(SuggestionsProperty);
        //     set => SetValue(SuggestionsProperty, value);
        // }

        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);

            // if (change.Property == IsDialogOpenProperty)
            // {
            //     PseudoClasses.Set(":open", change.NewValue.GetValueOrDefault<bool>());
            // }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            // _wp = e.NameScope.Find<WrapPanel>("PART_Container");
            ;
            // var dt = new TagBoxViewModel();
            // DataContext = dt;
            // dt.AppendedObject = "Always at the end";
            //
            // dt.Tags = new ObservableCollection<object>();
            //
            // dt.Tags.AddRange(new List<object>
            // {
            //     new TagViewModel("asdasdas"),
            //     new TagViewModel("asdasdas"),
            //     new TagViewModel("asdasdas"),
            //     new TagViewModel("asdasdas"),
            //     new TagViewModel("asdasdas"),
            //     new TagViewModel("asdasdas"),
            //     new TagViewModel("asdasdas")
            // });
            //
            //
            // var b1 = e.NameScope.Find<Button>("PART_add");
            //
            // b1.Click += delegate { dt.Tags.Add(new TagViewModel("asdasdas")); };
            //
            // var b2 = e.NameScope.Find<Button>("PART_sub");
            //
            // b2.Click += delegate
            // {
            //     if (dt.Tags.Count > 0)
            //         dt.Tags.Remove(dt.Tags.Last());
            // };
            
            // // _tb = e.NameScope.Find<AutoCompleteBox>("PART_InputBox"); ;
            // // _tb.KeyUp += OnKeyPress;
            // // _tb.GetObservable<string>(TextBox.TextProperty).Subscribe(TextChanged);
        }

        // private void TextChanged(string obj)
        // {
        //     if (obj.Length > 1 && !string.IsNullOrEmpty(obj.Trim()) && obj.EndsWith(' '))
        //     {
        //         var index = _wp.Children.Count - 1;
        //
        //         _wp.Children.Insert(index, GenerateTag(obj, index));
        //
        //         Dispatcher.UIThread.InvokeAsync(() => { _tb.Text = string.Empty; });
        //     }
        // }

        // public IControl GenerateTag(string Text, int index)
        // {
        //     return new Border
        //     {
        //         BorderThickness = new Thickness(1),
        //         Margin = new Thickness(5),
        //         Padding = new Thickness(5),
        //         BorderBrush = new SolidColorBrush(Colors.LightGreen),
        //         Child = new TextBlock
        //         {
        //             HorizontalAlignment = HorizontalAlignment.Center,
        //             VerticalAlignment = VerticalAlignment.Center,
        //             Text = Text.Trim()
        //         }
        //     };
        // }
        //
        // private void OnKeyPress(object? sender, KeyEventArgs e)
        // {
        //     if (e.Key == Key.Back && _tb.Text.Length == 0 && _wp.Children.Count > 1)
        //     {
        //         _wp.Children.RemoveAt(_wp.Children.Count - 2);
        //     }
        //     else if (e.Key == Key.Enter && !string.IsNullOrEmpty(_tb.Text.Trim()))
        //     {
        //         var index = _wp.Children.Count - 1;
        //
        //         _wp.Children.Insert(index, GenerateTag(_tb.Text, index));
        //
        //         Dispatcher.UIThread.InvokeAsync(() => { _tb.Text = string.Empty; });
        //     }
        // }
    }
}