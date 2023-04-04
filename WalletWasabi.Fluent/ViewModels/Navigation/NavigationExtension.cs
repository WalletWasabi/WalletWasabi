using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public static class NavigationExtensions
{
	public static async Task<DialogResult<T>> NavigateDialogAsync<T>(
		this TargettedNavigationStack stack,
		DialogViewModelBase<T> dialog)
	{
		stack.To(dialog);

		var result = await dialog.GetDialogResultAsync();

		stack.Back();

		return result;
	}
}
