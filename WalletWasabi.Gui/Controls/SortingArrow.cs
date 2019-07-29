using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace WalletWasabi.Gui.Controls
{
	public enum SortOrder
	{
		None,
		Increasing,
		Decreasing
	}

	public class SortingArrow : Button, IStyleable
	{
		Type IStyleable.StyleKey => typeof(Button);
		private static Geometry UpArrow { get; } = Geometry.Parse("M13.889,11.611c-0.17,0.17-0.443,0.17-0.612,0l-3.189-3.187l-3.363,3.36c-0.171,0.171-0.441,0.171-0.612,0c-0.172-0.169-0.172-0.443,0-0.611l3.667-3.669c0.17-0.17,0.445-0.172,0.614,0l3.496,3.493C14.058,11.167,14.061,11.443,13.889,11.611 M18.25,10c0,4.558-3.693,8.25-8.25,8.25c-4.557,0-8.25-3.692-8.25-8.25c0-4.557,3.693-8.25,8.25-8.25C14.557,1.75,18.25,5.443,18.25,10 M17.383,10c0-4.07-3.312-7.382-7.383-7.382S2.618,5.93,2.618,10S5.93,17.381,10,17.381S17.383,14.07,17.383,10");
		private static Geometry DownArrow { get; } = Geometry.Parse("M13.962,8.885l-3.736,3.739c-0.086,0.086-0.201,0.13-0.314,0.13S9.686,12.71,9.6,12.624l-3.562-3.56C5.863,8.892,5.863,8.611,6.036,8.438c0.175-0.173,0.454-0.173,0.626,0l3.25,3.247l3.426-3.424c0.173-0.172,0.451-0.172,0.624,0C14.137,8.434,14.137,8.712,13.962,8.885 M18.406,10c0,4.644-3.763,8.406-8.406,8.406S1.594,14.644,1.594,10S5.356,1.594,10,1.594S18.406,5.356,18.406,10 M17.521,10c0-4.148-3.373-7.521-7.521-7.521c-4.148,0-7.521,3.374-7.521,7.521c0,4.147,3.374,7.521,7.521,7.521C14.148,17.521,17.521,14.147,17.521,10");
		private Path IconPath { get; }
		private TextBlock TextBox { get; }

		public SortingArrow()
		{
			if (!Design.IsDesignMode)
			{
				Background = Application.Current.Resources[Global.ThemeBackgroundBrushResourceKey] as IBrush;
			}

			HorizontalContentAlignment = HorizontalAlignment.Stretch;
			IconPath = new Path
			{
				Stretch = Stretch.Fill,
				Stroke = Design.IsDesignMode ? Brushes.White : Application.Current.Resources[Global.ApplicationAccentForegroundBrushResourceKey] as IBrush,
				StrokeThickness = 0.8,
				Width = 10,
				Height = 10,
				Margin = new Thickness(7, 0),
				HorizontalAlignment = HorizontalAlignment.Right
			};

			TextBox = new TextBlock();

			Grid stackPnl = new Grid
			{
				Children =
				{
					new StackPanel
					{
						Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left,
						Children = { TextBox }
					},
					new StackPanel
					{
						Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
						Children =
						{
							IconPath
						}
					}
				}
			};

			Content = stackPnl;

			this.GetObservable(SortDirectionProperty).Subscribe(x =>
			{
				SortDirection = x;
			});
			this.GetObservable(TextProperty).Subscribe(x =>
			{
				Text = x;
			});
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);
			Click += SortingArrow_Click;
		}

		private void SortingArrow_Click(object sender, RoutedEventArgs e)
		{
			switch (SortDirection)
			{
				case SortOrder.None:
					SortDirection = SortOrder.Increasing;
					break;

				case SortOrder.Increasing:
					SortDirection = SortOrder.Decreasing;
					break;

				case SortOrder.Decreasing:
					SortDirection = SortOrder.Increasing;
					break;
			}
		}

		public static readonly StyledProperty<SortOrder> SortDirectionProperty =
		AvaloniaProperty.Register<SortingArrow, SortOrder>(nameof(SortDirection), defaultBindingMode: BindingMode.TwoWay);

		public SortOrder SortDirection
		{
			get => GetValue(SortDirectionProperty);
			set
			{
				SetValue(SortDirectionProperty, value);
				RefreshArrowIcon();
			}
		}

		public static readonly StyledProperty<string> TextProperty =
AvaloniaProperty.Register<SortingArrow, string>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

		public string Text
		{
			get => GetValue(TextProperty);
			set
			{
				SetValue(TextProperty, value);
				TextBox.Text = value;
			}
		}

		private void RefreshArrowIcon()
		{
			switch (SortDirection)
			{
				case SortOrder.None:
					IconPath.Data = Geometry.Parse("");
					break;

				case SortOrder.Increasing:
					IconPath.Data = DownArrow;
					break;

				case SortOrder.Decreasing:
					IconPath.Data = UpArrow;
					break;
			}
		}
	}
}
