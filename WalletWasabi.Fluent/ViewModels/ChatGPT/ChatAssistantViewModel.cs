using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ChatGPT;
using ChatGPT.ViewModels.Chat;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Newtonsoft.Json;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.ChatGPT.Messages;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT;

public partial class ChatAssistantViewModel : ReactiveObject
{
	static ChatAssistantViewModel()
	{
		Defaults.ConfigureDefaultServices();
	}

	private static string[] WelcomeMessages =
	{
		"What do you wish my master",
		"Speak, and I shall obey",
		"Your wish is my command"
	};

	private ChatViewModel? _chat;
	private CancellationTokenSource? _cts;
	[AutoNotify] private bool _isChatListVisible;
	[AutoNotify] private bool _isBusy;
	[AutoNotify] private string? _inputText;
	[AutoNotify] private string? _welcomeMessage;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _hasResults;
	[AutoNotify] private MessageViewModel? _currentMessage;

	public ChatAssistantViewModel()
	{
		void SwitchWelcomeMessage()
		{
			var index = Random.Shared.Next(0, WelcomeMessages.Length);
			WelcomeMessage = WelcomeMessages[index];
			Console.WriteLine($"WelcomeMessage='{WelcomeMessage}'");
		}

		_inputText = "";

		Messages = new ObservableCollection<MessageViewModel>();
		CurrentMessage = null;

		SwitchWelcomeMessage();
		CreateChat();

		this.WhenAnyValue(x => x.Messages.Count)
			.Select(x => x > 0)
			.Subscribe(x => HasResults = x);

		this.WhenAnyValue(x => x.IsChatListVisible)
			.Where(x => x)
			.Subscribe(_ => SwitchWelcomeMessage());

		Services.UiConfig.WhenAnyValue(
			x => x.Model,
			x => x.ApiKey)
			.Throttle(TimeSpan.FromMilliseconds(500))
			.Skip(1)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => CreateChat());

		SendCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var inputText = InputText;

			if (!string.IsNullOrWhiteSpace(inputText))
			{
				InputText = "";

				await SendAsync(inputText);
			}
		});

		ClearCommand = ReactiveCommand.Create(() =>
		{
			CreateChat();
		});
	}

	private void CreateChat()
	{
		try
		{
			_cts?.Cancel();
			_cts = null;
		}
		catch (Exception)
		{
			// ignored
		}

		IsBusy = false;
		Messages.Clear();
		CurrentMessage = null;

		var model = string.IsNullOrWhiteSpace(Services.UiConfig.Model)
			? "gpt-3.5-turbo"
			: Services.UiConfig.Model;

		var apiKey = string.IsNullOrWhiteSpace(Services.UiConfig.ApiKey)
			? ""
			: Services.UiConfig.ApiKey;

		_chat = new ChatViewModel
		{
			Settings = new ChatSettingsViewModel
			{
				Temperature = 0.7m,
				TopP = 1m,
				MaxTokens = 2000,
				Model = model,
				ApiKey = apiKey
			}
		};

		_chat.AddSystemMessage(_initialDirections);
	}

	public ICommand? SendCommand { get; protected set; }

	public ICommand? ClearCommand { get; protected set; }

	public ObservableCollection<MessageViewModel> Messages { get; }

	private async Task SendAsync(string input)
	{
		if (_chat == null)
		{
			return;
		}

		IsBusy = true;

		try
		{
			Messages.Add(new UserMessageViewModel()
			{
				Message = input
			});
			CurrentMessage = Messages.LastOrDefault();

			_chat.AddUserMessage(input);

			_cts = new CancellationTokenSource();
			var result = await _chat.SendAsync(_chat.CreateChatMessages(), _cts.Token);

			if (result?.Message is { } assistantResultString)
			{
				_chat.AddAssistantMessage(assistantResultString);

				Console.WriteLine(assistantResultString);

				AssistantResult? assistantResult;
				string resultMessage = "";

				try
				{
					assistantResult = JsonConvert.DeserializeObject<AssistantResult>(assistantResultString);
					if (assistantResult is { })
					{
						var message = assistantResult.Message;

						if (assistantResult.Status == "command")
						{
							if (message is { })
							{
								var globals = new ChatAssistantScriptGlobals
								{
									Chat = this,
									Main = MainViewModel.Instance
								};

								try
								{
									resultMessage = await CSharpScript.EvaluateAsync<string>(message, globals: globals);
								}
								catch (Exception e)
								{
									Console.WriteLine(e);
									resultMessage = "Failed to execute command.";
								}
								// TODO: Handle script result.
								/*
								if (resultMessage is null)
								{
									// TODO: "Error" message view model
									Messages.Add(new AssistantMessageViewModel
									{
										Message = resultMessage
									});
								}
								*/
							}
						}
						else if (assistantResult.Status == "error")
						{
							if (message is { })
							{
								resultMessage = message;
							}
							else
							{
								// TODO:
								resultMessage = message;
							}

							// TODO: "Error" message view model
							Messages.Add(new AssistantMessageViewModel
							{
								Message = resultMessage
							});
							CurrentMessage = Messages.LastOrDefault();
						}
						else if (assistantResult.Status == "message")
						{
							if (message is { })
							{
								resultMessage = message;
							}
							else
							{
								// TODO:
								resultMessage = message;
							}


							// TODO: "Message" message view model
							Messages.Add(new AssistantMessageViewModel
							{
								Message = resultMessage
							});
							CurrentMessage = Messages.LastOrDefault();
						}
					}
					else
					{
						// TODO: "Error" or "Assistant" message view model
						Messages.Add(new AssistantMessageViewModel
						{
							Message = resultMessage
						});
						CurrentMessage = Messages.LastOrDefault();
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					resultMessage = assistantResultString;
					//resultMessage = $"Error: {e.Message}";

					// TODO: "Error" message view model
					Messages.Add(new ErrorMessageViewModel()
					{
						Message = resultMessage
					});
					CurrentMessage = Messages.LastOrDefault();
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: " + ex.Message);
		}

		IsBusy = false;
	}
}
