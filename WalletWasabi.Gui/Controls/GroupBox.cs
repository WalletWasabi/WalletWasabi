using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls
{
	public class GroupBox : ContentControl, IStyleable
	{
		private ContentPresenter _titlePresenter;
		private Border _border;

		Type IStyleable.StyleKey => typeof(GroupBox);

		public static readonly StyledProperty<object> TitleProperty =
			AvaloniaProperty.Register<GroupBox, object>(nameof(Title));

		public object Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}

		protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
		{
			base.OnTemplateApplied(e);

			_titlePresenter = e.NameScope.Find<ContentPresenter>("PART_TitlePresenter");

			_border = e.NameScope.Find<Border>("PART_Border");
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			_border.Margin = new Thickness(0, -(_titlePresenter.DesiredSize.Height / 3), 0, 0);
			_border.Padding = new Thickness(0, (_titlePresenter.DesiredSize.Height / 3), 0, 0);

			_titlePresenter.Margin = new Thickness(Padding.Left, 0, 0, 0);

			return base.ArrangeOverride(finalSize);
		}
	}
}
