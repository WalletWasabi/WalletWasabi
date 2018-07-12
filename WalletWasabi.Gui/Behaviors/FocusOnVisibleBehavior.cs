using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;

namespace WalletWasabi.Gui.Behaviors
{
    class FocusOnVisibleBehavior : Behavior<Control>
    {
        private CompositeDisposable _disposables = new CompositeDisposable();
        private Control _attachedControl;

        static readonly AvaloniaProperty<string> AttachedControlNameProperty = AvaloniaProperty.Register<FocusOnVisibleBehavior, string>(nameof(AttachedControlName));

        public string AttachedControlName
        {
            get { return GetValue(AttachedControlNameProperty); }
            set { SetValue(AttachedControlNameProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.AttachedToLogicalTree += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(AttachedControlName))
                {
                    _attachedControl = AssociatedObject.FindControl<Control>(AttachedControlName);

                    if(_attachedControl == null)
                    {
                        throw new Exception($"Control: {AttachedControlName} was not found on the control.");
                    }

                    _disposables.Add(_attachedControl.GetObservable(Control.IsVisibleProperty).Subscribe(visible =>
                    {
                        if (visible)
                        {
                            AssociatedObject.Focus();
                        }
                    }));
                }
                else
                {
                    _disposables.Add(AssociatedObject.GetObservable(Control.IsVisibleProperty).Subscribe(visible =>
                    {
                        if (visible)
                        {
                            AssociatedObject.Focus();
                        }
                    }));
                }
            };
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            _disposables.Dispose();
        }
    }
}
