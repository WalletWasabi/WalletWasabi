using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Popup
{
    public interface IPopupHost
    {
       void SetDialog(IPopupView targetView);
       
       bool CanDisplayDialog { get; }
       
       void Close();
    }
}