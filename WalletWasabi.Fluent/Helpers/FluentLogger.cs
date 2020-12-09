using System;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers
{
	public static class FluentLogger
	{
		public static void ShowAndLogError(Exception ex)
		{
			Logger.LogError(ex);
			var dialog = new ShowErrorDialogViewModel(ex.Message);
			dialog.ShowDialogAsync();
		}
	}
}