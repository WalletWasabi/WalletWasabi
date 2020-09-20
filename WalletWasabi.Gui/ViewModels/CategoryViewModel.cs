namespace WalletWasabi.Gui.ViewModels
{
	public class CategoryViewModel : ViewModelBase
	{
		public CategoryViewModel(string title)
		{
			Title = title;
		}

		public string Title { get; }

		public virtual void OnCategorySelected()
		{
		}
	}
}
