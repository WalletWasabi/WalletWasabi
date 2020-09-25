using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Popup
{
    public interface IPopupHost
    {
       public void SetDialog(IPopupView targetView);
       
       public bool CanDisplayDialog { get; }
       
       public void Close();
    }
}