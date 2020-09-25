using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Popup;

namespace WalletWasabi.Fluent.ViewModels
{
    public class ViewModelBase : ReactiveObject, IPopupBase
    {
        private static IPopupHost _host;

        public void SetHost(IPopupHost host)
        {
            if (_host is null)
            {
                _host = host;
            }
            else
            {
                throw  new InvalidOperationException("The popup host has already been set.");
            }
        }

        public void SetDialog(IPopupView targetView) => _host?.SetDialog(targetView);

        public bool CanDisplayDialog => _host?.CanDisplayDialog ?? false;
    }
}
