using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Popup
{
    public interface IPopupBase
    {
        public void SetHost(IPopupHost host);
        
        public void SetDialog(IPopupView targetView);
        
        public bool CanDisplayDialog { get; }
    }
}