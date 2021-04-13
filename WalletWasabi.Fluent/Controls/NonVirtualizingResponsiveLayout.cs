using System;
using Avalonia;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Controls
{
    public class NonVirtualizingResponsiveLayout : NonVirtualizingLayout
    {
	    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
        {
	        // TODO: Implement MeasureOverride
	        return Size.Empty;
        }

        protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
        {
	        // TODO: Implement ArrangeOverride
	        return Size.Empty;
        }
    }
}
