using Microsoft.Extensions.Hosting;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using WalletWasabi.Services.Terminate;

namespace WalletWasabi.Rpc;

public class JsonRpcServer : BackgroundService
{
	public JsonRpcServer(IJsonRpcService service, JsonRpcServerConfiguration config, TerminateService terminateService)
	{
		_config = config;
		_terminateService = terminateService;
		_requestHandler = new JsonRpcRequestHandler<IJsonRpcService>(service, config.Network);

		_listener = new HttpListener();
		_listener.AuthenticationSchemes = AuthenticationSchemes.Basic;

		foreach (var prefix in _config.Prefixes)
		{
			_listener.Prefixes.Add(prefix);
		}
	}

	private readonly TerminateService _terminateService;
	private readonly HttpListener _listener;
	private readonly JsonRpcRequestHandler<IJsonRpcService> _requestHandler;
	private readonly JsonRpcServerConfiguration _config;

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		_listener.Start();
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);

		// HttpListener is disposable but the dispose method is not public.
		// That's a quirk of the HttpListener implementation.
		_listener.Stop();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		bool stopRpcRequestReceived = false;

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var context = await _listener.GetContextAsync().WaitAsync(stoppingToken).ConfigureAwait(false);
				var request = context.Request;
				var response = context.Response;

				if (request.HttpMethod == "POST")
				{
					// Credentials are checked before the body is read, so an unauthenticated client cannot make the server buffer it.
					if (IsAuthorized(context))
					{
						using var reader = new StreamReader(request.InputStream);
						string body = await reader.ReadToEndAsync(stoppingToken).ConfigureAwait(false);

						var path = request.Url?.LocalPath ?? string.Empty;
						string jsonResponse = string.Empty;

						if (!JsonRpcRequest.TryParse(body, out var allRpcRequests, out var isBatch))
						{
							jsonResponse = _requestHandler.CreateParseErrorResponse();
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
								jsonResponse = await _requestHandler.HandleRequestsAsync(path, requestsToProcess, isBatch, stoppingToken).ConfigureAwait(false);
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
			_terminateService.SignalForceTerminate();
		}
	}

	private bool IsAuthorized(HttpListenerContext context) =>
		context.User?.Identity is HttpListenerBasicIdentity identity
		&& CredentialsMatch(identity.Name, identity.Password, _config.JsonRpcUser, _config.JsonRpcPassword);

	/// <summary>Compares SHA-256 digests in fixed time, so neither the content nor the length of a credential leaks through timing.</summary>
	internal static bool CredentialsMatch(string? user, string? password, string? expectedUser, string? expectedPassword)
	{
		// Single & on purpose: both comparisons always run.
		return Matches(user, expectedUser) & Matches(password, expectedPassword);

		static bool Matches(string? actual, string? expected) =>
			CryptographicOperations.FixedTimeEquals(
				SHA256.HashData(Encoding.UTF8.GetBytes(actual ?? "")),
				SHA256.HashData(Encoding.UTF8.GetBytes(expected ?? "")));
	}
}
