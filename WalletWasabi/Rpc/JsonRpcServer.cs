using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Logging;

namespace WalletWasabi.Rpc;

public class JsonRpcServer : BackgroundService
{
	public JsonRpcServer(IJsonRpcService service, JsonRpcServerConfiguration config)
	{
		Config = config;
		Listener = new HttpListener();
		Listener.AuthenticationSchemes = AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;
		foreach (var prefix in Config.Prefixes)
		{
			Listener.Prefixes.Add(prefix);
		}
		Service = service;
	}

	private HttpListener Listener { get; }
	private IJsonRpcService Service { get; }
	private JsonRpcServerConfiguration Config { get; }

	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		Listener.Start();
		await base.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);
		Listener.Stop();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var handler = new JsonRpcRequestHandler<IJsonRpcService>(Service);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var context = await GetHttpContextAsync(stoppingToken).ConfigureAwait(false);
				var request = context.Request;
				var response = context.Response;

				if (request.HttpMethod == "POST")
				{
					using var reader = new StreamReader(request.InputStream);
					string body = await reader.ReadToEndAsync().ConfigureAwait(false);

					if (IsAuthorized(context))
					{
						var result = await handler.HandleAsync(body, stoppingToken).ConfigureAwait(false);

						// result is null only when the request is a notification.
						if (!string.IsNullOrEmpty(result))
						{
							response.ContentType = "application/json-rpc";
							var output = response.OutputStream;
							var buffer = Encoding.UTF8.GetBytes(result);
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

		var identity = (HttpListenerBasicIdentity?) user.Identity;
		return CheckValidCredentials(identity);
	}

	private async Task<HttpListenerContext> GetHttpContextAsync(CancellationToken cancellationToken)
	{
		var getHttpContextTask = Listener.GetContextAsync();
		var tcs = new TaskCompletionSource<bool>();
		using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state).TrySetResult(true), tcs))
		{
			var firstTaskToComplete = await Task.WhenAny(getHttpContextTask, tcs.Task).ConfigureAwait(false);
			if (getHttpContextTask != firstTaskToComplete)
			{
				cancellationToken.ThrowIfCancellationRequested();
			}
		}
		return await getHttpContextTask.ConfigureAwait(false);
	}

	private bool CheckValidCredentials(HttpListenerBasicIdentity? identity)
	{
		return identity is { } && (identity.Name == Config.JsonRpcUser && identity.Password == Config.JsonRpcPassword);
	}
}
