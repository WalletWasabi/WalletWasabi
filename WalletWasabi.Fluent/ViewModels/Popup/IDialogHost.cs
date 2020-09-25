using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Dialog
{
    public interface IDialogHost
    {
       void SetDialog(IDialogView targetView);
       
       bool CanDisplayDialog { get; }
       
       void Close();
    }
}