using ReactiveUI;
using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Models;
namespace WalletWasabi.Fluent.ViewModels
{
	public class SettingsPageViewModel : NavBarItemViewModel
	{
		private string _randomString;

		public SettingsPageViewModel(IScreen screen) : base(screen)
		{
			Title = "Settings";

			NextCommand = ReactiveCommand.Create(() => screen.Router.Navigate.Execute(new HomePageViewModel(screen)));

			OpenDialogCommand = ReactiveCommand.CreateFromTask(async () => await ConfirmSetting.Handle("Please confirm the setting:").ToTask());

			ConfirmSetting = new Interaction<string, bool>();

			ConfirmSetting.RegisterHandler(
				async interaction =>
				{
					var x = new TestDialogViewModel(interaction.Input);
					var result = await x.ShowDialogAsync(MainViewModel.Instance);
					interaction.SetOutput(result);
				});

			// For TextBox error look
			this.ValidateProperty(x => x.RandomString, (errors) => errors.Add(ErrorSeverity.Error, "Random Error Message"));
			this.RaisePropertyChanged(nameof(RandomString));
		}

		public string RandomString
		{
			get => _randomString;
			set => this.RaiseAndSetIfChanged(ref _randomString, value);
		}

		public ICommand NextCommand { get; }
		public ICommand OpenDialogCommand { get; }
		public Interaction<string, bool> ConfirmSetting { get; }

		public override string IconName => "settings_regular";
	}
}