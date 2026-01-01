using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Scheme;

namespace WalletWasabi.Fluent.ViewModels.Scheme;

[NavigationMetaData(
	Title = "Scripting",
	Caption = "Automate Wasabi",
	IconName = "nav_wallet_24_regular",
	Order = 3,
	Category = "General",
	Keywords = ["Wallet", "Coins", "UTXO", "Scripting"],
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen,
	Searchable = true)]
public partial class SchemeConsoleViewModel : RoutableViewModel
{
	private readonly Daemon.Scheme _schemeInterpreter;
	[AutoNotify] private string _commandInput;
	[AutoNotify] private bool _isExecuting;
	public ObservableCollection<SchemeOutput> Output { get; private set; }
	public ObservableCollection<string> CommandHistory { get; }

	public ICommand ExecuteCommand { get; private set; }

	public SchemeConsoleViewModel(Daemon.Scheme schemeInterpreter)
	{
		_schemeInterpreter = schemeInterpreter;
		CommandInput = string.Empty;
		CommandHistory = new();
		Output = new();
		IsExecuting = false;
		ExecuteCommand = ReactiveCommand.CreateFromTask(ExecuteCommandAsync);
		SetupCancel(enableCancel: true, enableCancelOnEscape: false, enableCancelOnPressed: true);
	}

	private async Task ExecuteCommandAsync()
	{
		string? command = CommandInput?.Trim();
		if (string.IsNullOrEmpty(command))
		{
			return;
		}

		Output.Add(new SchemeOutputCommand(command));
		CommandInput = string.Empty;

		IsExecuting = true;

		try
		{
			string result = await RunCommandAsync(command);
			Output.Add(new SchemeOutputResult(result));
		}
		catch (Exception ex)
		{
			Output.Add(new SchemeOutputError(ex.Message));
		}
		finally
		{
			IsExecuting = false;
		}
	}

	private async Task<string> RunCommandAsync(string command)
	{
		try
		{
			Expression expressionResult = await _schemeInterpreter.Execute(command);
			return _schemeInterpreter.ToJson(expressionResult);
		}
		catch (Exception ex)
		{
			throw new Exception($"Failed to execute command: {ex.Message}");
		}
	}
}
