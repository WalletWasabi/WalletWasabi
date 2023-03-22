using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AI.Model.Services;
using AI.Services;
using ChatGPT.Model.Services;
using ChatGPT.Services;
using ChatGPT.ViewModels.Chat;
using ChatGPT.ViewModels.Settings;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT;

public partial class ChatAssistantViewModel : ReactiveObject
{
	static ChatAssistantViewModel()
	{
		ConfigureServices();
	}

	private string _initialDirections = """
You are a helpful assistant named Wasabito, you are Wasabi Wallet operator.
I will write text prompts and you will generate appropriate answers in json.

Write answers as json:
{
  "status": "",
  "command": "",
}

Where "status" value is:
- "command":  when command is an answer
- "error": when not possible to answer
- "message": when answer is valid

Where "command" value is only set when "status"="command"
only following commands are available: "send", "receive", "balance"

- send command requires address and amount
- receive command returns only address, does not require any params
- balance command returns only BTC or USD balance value, does not require any params

If user does not provide valid param to execute command please ask followup question to provide that info.

If not enough info is provided to execute command please set status=error and ask user to provide missing information.

Never say you can not execute command, just return json with proper status.
""";

	private ChatViewModel _chat;
	private CancellationTokenSource _cts;

	[AutoNotify] private bool _isChatListVisible;
	[AutoNotify] private string? _inputText = "";

	public ChatAssistantViewModel()
	{
		_chat = new ChatViewModel
		{
			Settings = new ChatSettingsViewModel
			{
				Temperature = 0.7m,
				TopP = 1m,
				MaxTokens = 2000,
				Model = "gpt-3.5-turbo"
			}
		};

		_chat.AddSystemMessage(_initialDirections);

		SendCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var inputText = InputText;

			if (!string.IsNullOrWhiteSpace(inputText))
			{
				InputText = "";

				await SendAsync(inputText);
			}
		});

		Messages = new ObservableCollection<MessageViewModel>();
	}

	public ICommand? SendCommand { get; protected set; }

	public ObservableCollection<MessageViewModel> Messages { get; }

	private async Task SendAsync(string input)
	{
		try
		{
			Messages.Add(new UserMessageViewModel()
			{
				Message = input
			});

			_chat.AddUserMessage(input);

			_cts = new CancellationTokenSource();
			var result = await _chat.SendAsync(_chat.CreateChatMessages(), _cts.Token);

			_chat.AddAssistantMessage(result?.Message);

			Console.WriteLine(result?.Message);

			// TODO: Deserialize result json message and get message param and command.

			// TODO: AssistantResult

			var resultMessage = result?.Message;

			Messages.Add(new AssistantMessageViewModel
			{
				Message = resultMessage
			});
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: " + ex.Message);
		}
	}

	private static void ConfigureServices()
	{
		IServiceCollection serviceCollection = new ServiceCollection();

		serviceCollection.AddSingleton<IStorageFactory, IsolatedStorageFactory>();
		serviceCollection.AddSingleton<IChatService, ChatService>();

		serviceCollection.AddTransient<ChatMessageViewModel>();
		serviceCollection.AddTransient<ChatSettingsViewModel>();
		serviceCollection.AddTransient<ChatResultViewModel>();
		serviceCollection.AddTransient<ChatViewModel>();
		serviceCollection.AddTransient<PromptViewModel>();

		Ioc.Default.ConfigureServices(serviceCollection.BuildServiceProvider());
	}
}
