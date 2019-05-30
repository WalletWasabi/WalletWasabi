using Avalonia.Controls;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Behaviors
{
    public class CommandOnDoubleClickBehavior : CommandBasedBehavior<Control>
    {
        private CompositeDisposable _disposables;

        protected override void OnAttached()
        {
            _disposables = new CompositeDisposable();

            base.OnAttached();

            _disposables.Add(AssociatedObject.AddHandler(Control.PointerPressedEvent, (sender, e) =>
            {
                if(e.ClickCount == 2)
                {
                    e.Handled = ExecuteCommand();
                }
            }));
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            _disposables.Dispose();
        }
    }
}
