using System.Collections.ObjectModel;
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

	private Subject<bool> _hasResultsSubject;
	private ChatViewModel? _chat;
	private CancellationTokenSource? _cts;
	[AutoNotify] private bool _isChatListVisible;
	[AutoNotify] private string? _inputText = "";

	public ChatAssistantViewModel()
	{
		_hasResultsSubject = new Subject<bool>();
		_hasResultsSubject.OnNext(false);

		CreateChat();

		Messages = new ObservableCollection<MessageViewModel>();

		HasResults = _hasResultsSubject.Select(x => x).Replay(1).RefCount();

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
			Messages.Clear();
			_hasResultsSubject.OnNext(false);
		});
	}

	private void CreateChat()
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
	}

	public IObservable<bool> HasResults { get; }

	public ICommand? SendCommand { get; protected set; }

	public ICommand? ClearCommand { get; protected set; }

	public ObservableCollection<MessageViewModel> Messages { get; }

	private async Task SendAsync(string input)
	{
		if (_chat == null)
		{
			return;
		}

		try
		{
			Messages.Add(new UserMessageViewModel()
			{
				Message = input
			});

			if (Messages.Count >= 1)
			{
				_hasResultsSubject.OnNext(true);
			}

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
								resultMessage = await CSharpScript.EvaluateAsync<string>(message, globals: globals);

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
						}
					}
					else
					{
						// TODO: "Error" or "Assistant" message view model
						Messages.Add(new AssistantMessageViewModel
						{
							Message = resultMessage
						});
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					resultMessage = assistantResultString;

					// TODO: "Error" message view model
					Messages.Add(new ErrorMessageViewModel()
					{
						Message = resultMessage
					});
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: " + ex.Message);
		}
	}
}
