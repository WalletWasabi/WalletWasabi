using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using Dock.Model;
using ReactiveUI;
using System;

namespace WalletWasabi.Gui.ViewModels
{
    public abstract class WasabiDocumentTabViewModel : ViewModelBase, IDocumentTabViewModel
	{
		public WasabiDocumentTabViewModel(Global global, string title)
		{
			Title = title;
			Global = global ?? throw new ArgumentNullException(nameof(global));
		}

		public WasabiDocumentTabViewModel(Global global)
		{
			Global = global ?? throw new ArgumentNullException(nameof(global));
		}

		public Global Global { get; }
		public Guid Id { get; set; } = Guid.NewGuid();
		public virtual string Title { get; set; }
		public object Context { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public IView Parent { get; set; }
		private bool _isSelected;
		private bool _isClosed;

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
		}

		public bool IsClosed
		{
			get => _isClosed;
			set => this.RaiseAndSetIfChanged(ref _isClosed, value);
		}

		public bool IsDirty { get; set; }

		public virtual void OnSelected()
		{
			IsSelected = true;
		}

		public virtual void OnDeselected()
		{
			IsSelected = false;
		}

		public virtual void OnOpen()
		{
			IsClosed = false;
		}

		public virtual bool OnClose()
		{
			IsSelected = false;
			IoC.Get<IShell>().RemoveDocument(this);
			IsClosed = true;
			return true;
		}
	}
}
