using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.ManagedDialogs
{ 
	class ManagedFileChooserItemViewModel : ViewModelBase
    {
        private string _displayName;
        private string _path;
        private bool _isDirectory;

        public string DisplayName
        {
            get => _displayName;
            set => this.RaiseAndSetIfChanged(ref _displayName, value);
        }

        public string Path
        {
            get => _path;
            set => this.RaiseAndSetIfChanged(ref _path, value);
        }

        public string IconKey => IsDirectory ? "Icon_Folder" : "Icon_File";

        public bool IsDirectory
        {
            get => _isDirectory;
            set
            {
				if (this.RaiseAndSetIfChanged(ref _isDirectory, value))
				{
					this.RaisePropertyChanged(nameof(IconKey));
				}
            }
        }

        public ManagedFileChooserItemViewModel()
        {
                
        }

        public ManagedFileChooserItemViewModel(ManagedFileChooserNavigationItem item)
        {
            IsDirectory = true;
            Path = item.Path;
            DisplayName = item.DisplayName;
        }
    }
}
