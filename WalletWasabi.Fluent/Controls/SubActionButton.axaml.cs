using System.Collections;
using System.Reactive.Disposables;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls
{
    public class SubActionButton : ContentControl
    {
        public static readonly StyledProperty<Panel> PanelIconProperty =
            AvaloniaProperty.Register<SubActionButton, Panel>(nameof(PanelIcon));

        public Panel PanelIcon
        {
            get => GetValue(PanelIconProperty);
            set => SetValue(PanelIconProperty, value);
        }

        public static readonly StyledProperty<PathIcon> IconProperty =
            AvaloniaProperty.Register<SubActionButton, PathIcon>(nameof(Icon));

        public PathIcon Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly StyledProperty<ICommand> CommandProperty =
            AvaloniaProperty.Register<SubActionButton, ICommand>(nameof(Command));

        public static readonly StyledProperty<UICommandCollection> SubCommandsProperty =
            AvaloniaProperty.Register<SubActionButton, UICommandCollection>(nameof(SubCommands));

        public static readonly StyledProperty<IEnumerable> ItemsProperty =
            AvaloniaProperty.Register<SubActionButton, IEnumerable>(nameof(Items));

        public ICommand Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public UICommandCollection SubCommands
        {
            get => GetValue(SubCommandsProperty);
            set => SetValue(SubCommandsProperty, value);
        }

        public IEnumerable Items
        {
            get => GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
	        base.OnAttachedToVisualTree(e);

	        if (PanelIcon is null)
	        {
		        return;
	        }

	        foreach (var child in PanelIcon.Children)
	        {
		        child?.WhenAnyValue(x => x.IsVisible)
			        .Subscribe(x => UpdateIconFromPanelIcon());
	        }
        }

        private void UpdateIconFromPanelIcon()
        {
            foreach (var child in PanelIcon.Children)
            {
                if (child is PathIcon { IsVisible: true } pathIcon)
                {
                    Icon = pathIcon;
                    break;
                }
            }
        }
    }
}
