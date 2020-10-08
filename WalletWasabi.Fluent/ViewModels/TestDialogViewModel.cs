using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels
{
	public class TestDialogViewModel : DialogViewModelBase<string>
	{
		protected override void DialogClosing()
		{
		}

		protected override void DialogShowing()
		{
			Task.Run(async () =>
			{
				await Task.Delay(15000);
				CloseDialog("Done");
			});
		}
	}
}
