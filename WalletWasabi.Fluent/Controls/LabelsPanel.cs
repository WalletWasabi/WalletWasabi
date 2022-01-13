using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls
{
    public class LabelsPanel : VirtualizingStackPanel
    {
        public static readonly StyledProperty<Control?> EllipsisControlProperty =
            AvaloniaProperty.Register<LabelsPanel, Control?>(nameof(EllipsisControl));

        public Control? EllipsisControl
        {
            get => GetValue(EllipsisControlProperty);
            set => SetValue(EllipsisControlProperty, value);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (EllipsisControl is { } ellipsisControl)
            {
                ((ISetLogicalParent)ellipsisControl).SetParent(this);
                VisualChildren.Add(ellipsisControl);
                LogicalChildren.Add(ellipsisControl);
            }

            base.OnAttachedToVisualTree(e);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var ellipsis = 0.0;
            if (EllipsisControl is { })
            {
                EllipsisControl.Measure(availableSize);
                ellipsis = EllipsisControl.DesiredSize.Width;
            }

            return base.MeasureOverride(availableSize.WithWidth(availableSize.Width + ellipsis));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var spacing = Spacing;
            var ellipsisWidth = 0.0;
            var width = 0.0;
            var height = 0.0;
            var finalWidth = finalSize.Width;
            var showEllipsis = false;
            var totalChildren = Children.Count;
            var count = 0;

            if (EllipsisControl is { })
            {
                ellipsisWidth = EllipsisControl.DesiredSize.Width;
            }

            for (var i = 0; i < totalChildren; i++)
            {
                var child = Children[i];
                var childWidth = child.DesiredSize.Width;

                if (width + childWidth > finalWidth)
                {
                    while (true)
                    {
                        if (width + ellipsisWidth > finalWidth)
                        {
                            var previous = i - 1;
                            if (previous >= 0)
                            {
                                var previousChild = Children[previous];
                                count--;
                                width -= previousChild.DesiredSize.Width + spacing;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    showEllipsis = true;
                    if (EllipsisControl is { })
                    {
                        width += EllipsisControl.DesiredSize.Width;
                    }

                    break;
                }

                width += child.DesiredSize.Width + spacing;
                height = Math.Max(height, child.DesiredSize.Height);
                count++;
            }

            var offset = 0.0;

            for (var i = 0; i < totalChildren; i++)
            {
                var child = Children[i];
                if (i < count)
                {
                    var rect = new Rect(offset, 0.0, child.DesiredSize.Width, height);
                    child.Arrange(rect);
                    offset += child.DesiredSize.Width + spacing;
                }
                else
                {
                    child.Arrange(new Rect(-10000, -10000, 0, 0));
                }
            }

            if (EllipsisControl is { })
            {
                if (showEllipsis)
                {
                    var rect = new Rect(offset, 0.0, EllipsisControl.DesiredSize.Width, height);
                    EllipsisControl.Arrange(rect);
                }
                else
                {
                    EllipsisControl.Arrange(new Rect(-10000, -10000, 0, 0));
                }
            }

            return new Size(width, height);
        }
    }
}
