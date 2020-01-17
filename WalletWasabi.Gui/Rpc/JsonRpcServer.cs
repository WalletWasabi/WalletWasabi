using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Logging;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Rpc
{
	public class JsonRpcServer : BackgroundService
	{
		private CancellationTokenSource Cancellation { get; }

		private HttpListener Listener { get; }
		private WasabiJsonRpcService Service { get; }
		private JsonRpcServerConfiguration Config { get; }

		public JsonRpcServer(Global global, JsonRpcServerConfiguration config)
		{
			Config = config;
			Listener = new HttpListener();
			Listener.AuthenticationSchemes = AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;
			foreach (var prefix in Config.Prefixes)
			{
				Listener.Prefixes.Add(prefix);
			}
			Cancellation = new CancellationTokenSource();
			Service = new WasabiJsonRpcService(global);
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			Listener.Start();
			await base.StartAsync(cancellationToken);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken);
			Listener.Stop();
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var handler = new JsonRpcRequestHandler<WasabiJsonRpcService>(Service);

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					var context = await GetHttpContextAsync(stoppingToken);
					var request = context.Request;
					var response = context.Response;

					if (request.HttpMethod == "POST")
					{
						using var reader = new StreamReader(request.InputStream);
						string body = await reader.ReadToEndAsync().ConfigureAwait(false);

						var identity = (HttpListenerBasicIdentity)context.User?.Identity;
						if (!Config.RequiresCredentials || CheckValidCredentials(identity))
						{
							var result = await handler.HandleAsync(body, stoppingToken).ConfigureAwait(false);

							// result is null only when the request is a notification.
							if (!string.IsNullOrEmpty(result))
							{
								response.ContentType = "application/json-rpc";
								var output = response.OutputStream;
								var buffer = Encoding.UTF8.GetBytes(result);
								await output.WriteAsync(buffer, 0, buffer.Length, stoppingToken);
								await output.FlushAsync(stoppingToken);
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
			return await getHttpContextTask;
		}

		private bool CheckValidCredentials(HttpListenerBasicIdentity identity)
		{
			return identity is { } && (identity.Name == Config.JsonRpcUser && identity.Password == Config.JsonRpcPassword);
		}
	}
}
