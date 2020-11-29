using System;

namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public interface INavigationManager
	{
		/// <summary>
		/// The Current Page.
		/// </summary>
		INavigatable CurrentPage { get; }

		/// <summary>
		/// The Previous Page.
		/// </summary>
		INavigatable PreviousPage { get; }

		/// <summary>
		/// True if you can navigate back, else false.
		/// </summary>
		bool CanNavigateBack { get; }

		void Navigate(INavigatable viewmodel, NavigationMode mode = NavigationMode.Normal);

		void Back();

		void Back(Type pageType);
	}
}