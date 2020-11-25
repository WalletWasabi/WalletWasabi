using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels
{
	public static class RoutableExtensions
	{
		public static async Task<TResult> NavigateDialog<TResult>(this RoutableViewModel vm, DialogViewModelBase<TResult> dialog)
		{
			TResult result;

			using (vm.NavigateTo(dialog, NavigationTarget.DialogScreen))
			{
				result = await dialog.GetDialogResultAsync();
			}

			return result;
		}
	}
}