using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.ViewModels
{
	public class TestDialogViewModel : DialogViewModelBase<string>
	{
		public TestDialogViewModel(MainViewModel mainViewModel) : base(mainViewModel)
		{
		}

		protected override void DialogShown()
		{
			Task.Run(async () =>
			{
				await Task.Delay(5000);
				CloseDialog("Done");
			});
		}
	}
}
