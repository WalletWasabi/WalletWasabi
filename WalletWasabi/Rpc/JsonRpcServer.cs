using Microsoft.Extensions.Hosting;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;

namespace WalletWasabi.Rpc;

public class JsonRpcServer : BackgroundService
{
	public JsonRpcServer(IJsonRpcService service, JsonRpcServerConfiguration config, TerminateService terminateService)
	{
		Config = config;
		TerminateService = terminateService;
		RequestHandler = new JsonRpcRequestHandler<IJsonRpcService>(service);

		Listener = new HttpListener();
		Listener.AuthenticationSchemes = AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;

		foreach (var prefix in Config.Prefixes)
		{
			Listener.Prefixes.Add(prefix);
		}
	}

	private TerminateService TerminateService { get; }
	private HttpListener Listener { get; }
	private JsonRpcRequestHandler<IJsonRpcService> RequestHandler { get; }
	private JsonRpcServerConfiguration Config { get; }

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		Listener.Start();
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);

		// HttpListener is disposable but the dispose method is not public.
		// That's a quirk of the HttpListener implementation.
		Listener.Stop();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		bool stopRpcRequestReceived = false;

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var context = await Listener.GetContextAsync().WaitAsync(stoppingToken).ConfigureAwait(false);
				var request = context.Request;
				var response = context.Response;

				if (request.HttpMethod == "POST")
				{
					using var reader = new StreamReader(request.InputStream);
					string body = await reader.ReadToEndAsync(stoppingToken).ConfigureAwait(false);

					if (IsAuthorized(context))
					{
						var path = request.Url?.LocalPath ?? string.Empty;
						string jsonResponse = string.Empty;

						if (!JsonRpcRequest.TryParse(body, out var allRpcRequests, out var isBatch))
						{
							jsonResponse = RequestHandler.CreateParseErrorResponse();
						}
						else
						{
							JsonRpcRequest[] requestsToProcess = allRpcRequests.Where(x =>
							{
								bool isStopRequest = x.Method == IJsonRpcService.StopRpcCommand;

								if (isStopRequest)
								{
									stopRpcRequestReceived = true;
								}

								return !isStopRequest;
							}).ToArray();

							if (requestsToProcess.Length > 0)
							{
								jsonResponse = await RequestHandler.HandleRequestsAsync(path, requestsToProcess, isBatch, stoppingToken).ConfigureAwait(false);
							}
						}

						// result is null only when the request is a notification.
						if (!string.IsNullOrEmpty(jsonResponse))
						{
							response.ContentType = "application/json-rpc";
							var output = response.OutputStream;
							var buffer = Encoding.UTF8.GetBytes(jsonResponse);
							await output.WriteAsync(buffer.AsMemory(0, buffer.Length), stoppingToken).ConfigureAwait(false);
							await output.FlushAsync(stoppingToken).ConfigureAwait(false);
						}
					}
					else
					{
						response.StatusCode = (int)HttpStatusCode.Unauthorized;
					}
				}
				else
				{
					response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
				}

				response.Close();

				if (stopRpcRequestReceived)
				{
					break;
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		if (stopRpcRequestReceived)
		{
			Logger.LogDebug($"User sent '{IJsonRpcService.StopRpcCommand}' command. Terminating application.");
			TerminateService.SignalForceTerminate();
		}
	}

	private bool IsAuthorized(HttpListenerContext context)
	{
		if (!Config.RequiresCredentials)
		{
			return true;
		}

		var user = context.User;
		if (user is null)
		{
			return false;
		}

		var identity = (HttpListenerBasicIdentity?)user.Identity;
		return CheckValidCredentials(identity);
	}

	private bool CheckValidCredentials(HttpListenerBasicIdentity? identity)
	{
		return identity is { } && (identity.Name == Config.JsonRpcUser && identity.Password == Config.JsonRpcPassword);
	}
}
