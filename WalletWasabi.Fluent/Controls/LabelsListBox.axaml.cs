using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class LabelsListBox : ListBox, IStyleable
{
    Type IStyleable.StyleKey => typeof(LabelsListBox);

    protected override IItemContainerGenerator CreateItemContainerGenerator()
    {
        return new ItemContainerGenerator<LabelControl>(
            this,
            ContentControl.ContentProperty,
            ContentControl.ContentTemplateProperty);
    }
}
