using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using Dock.Model;
using ReactiveUI;

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class WasabiDocumentTabViewModel : ViewModelBase, IDocumentTabViewModel
	{
		public WasabiDocumentTabViewModel(string title)
		{
			Title = title;
		}

		public WasabiDocumentTabViewModel()
		{
		}

		public string Id { get; set; }
		public virtual string Title { get; set; }
		public object Context { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public IView Parent { get; set; }
		private bool _isSelected;

		public bool IsSelected
		{
			get { return _isSelected; }
			set { this.RaiseAndSetIfChanged(ref _isSelected, value); }
		}
		
		public virtual void Close()
		{
			IoC.Get<IShell>().RemoveDocument(this);
			this.IsSelected = false;
		}

		public virtual void OnSelected()
		{
			this.IsSelected = true;
		}

		public virtual void OnDeselected()
		{
			this.IsSelected = false;
		}
	}
}
