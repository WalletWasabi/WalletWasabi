using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Logging;
using WalletWasabi.TorControl;

namespace WalletWasabi.Gui.P2EP
{
	public class P2EPServer : BackgroundService
	{
		public P2EPServer(Global global)
		{
			Listener = new HttpListener();
			Listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
			Listener.Prefixes.Add($"http://{IPAddress.Loopback}:{37129}/");
			Global = global;
		}

		private HttpListener Listener { get; }
		public string PaymentEndpoint { get; private set; }
		public Global Global { get; }

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			while (!Global.TorManager.IsRunning)
			{
				await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
			}
			using( var torControlClient = new TorControlClient())
			{
				await torControlClient.ConnectAsync().ConfigureAwait(false);
				await torControlClient.AuthenticateAsync("MyLittlePonny").ConfigureAwait(false);
				PaymentEndpoint = await torControlClient.CreateHiddenServiceAsync().ConfigureAwait(false);
			}

			Listener.Start();
			await base.StartAsync(cancellationToken).ConfigureAwait(false);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken).ConfigureAwait(false);			
			Listener.Stop();
			var paymentEndpoint = PaymentEndpoint;
			if (!string.IsNullOrWhiteSpace(paymentEndpoint))
			{
				using( var torControlClient = new TorControlClient())
				{
					await torControlClient.ConnectAsync();
					await torControlClient.AuthenticateAsync("MyLittlePonny");
					await torControlClient.DestroyHiddenService(paymentEndpoint);
				}
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var handler = new P2EPRequestHandler(Global.Network);

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

						var identity = (HttpListenerBasicIdentity)context.User?.Identity;

						try
						{
							var result = await handler.HandleAsync(body, stoppingToken).ConfigureAwait(false);

							// result is null only when the request is a notification.
							var output = response.OutputStream;
							var buffer = Encoding.UTF8.GetBytes(result);
							await output.WriteAsync(buffer, 0, buffer.Length, stoppingToken).ConfigureAwait(false);
							await output.FlushAsync(stoppingToken).ConfigureAwait(false);
						}
						catch (P2EPException e)
						{
							response.StatusCode = (int)HttpStatusCode.BadRequest;
							response.StatusDescription = e.Message;
						}
					}
					else
					{
						response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
					}
					response.ContentType = "text/html; charset=UTF-8";
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
			return await getHttpContextTask.ConfigureAwait(false);
		}
	}
}
