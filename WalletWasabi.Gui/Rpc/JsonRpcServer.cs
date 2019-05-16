using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Rpc
{
	public class JsonRpcServer
	{
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;
		private CancellationTokenSource _cts { get; }

		private HttpListener _listener;
		private WasabiJsonRpcService _service;		
		private JsonRpcServerConfiguration _config;

		public JsonRpcServer(Global global, JsonRpcServerConfiguration config)
		{
			_config = config;
			_listener = new HttpListener();
			_listener.AuthenticationSchemes = AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;
			foreach(var prefix in _config.Prefixes)
			{
				_listener.Prefixes.Add(prefix);
			}
			_cts = new CancellationTokenSource();
			_service = new WasabiJsonRpcService(global);
		}

		public void Start()
		{
			Interlocked.Exchange(ref _running, 1);
			_listener.Start();

			Task.Run(async ()=>{
				try
				{
					var handler = new JsonRpcRequestHandler(_service);

					while (IsRunning)
					{
						var context = _listener.GetContext();
						var request = context.Request;
						var response = context.Response;

						if (request.HttpMethod == "POST")
						{
							string body;
							using(var reader = new StreamReader(request.InputStream))
								body = await reader.ReadToEndAsync();

							var identity = (HttpListenerBasicIdentity)context.User?.Identity;
							if (!_config.RequiresCredentials || CheckValidCredentials(identity))
							{
								var result = await handler.HandleAsync(body, _cts);
								
								// result is null only when the request is a notification.
								if(!string.IsNullOrEmpty(result))
								{
									response.ContentType = "application/json-rpc";
									var output = response.OutputStream;
									var buffer = Encoding.UTF8.GetBytes(result);
									await output.WriteAsync(buffer, 0, buffer.Length);
									await output.FlushAsync();
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
				}
				finally
				{
					Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
				}
			});
		}

		internal void Stop()
		{
			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.;
			_listener.Stop();
			_cts.Cancel();
			while (IsStopping)
			{
				Task.Delay(50).GetAwaiter().GetResult();
			}
			_cts.Dispose();
		}

		private bool CheckValidCredentials(HttpListenerBasicIdentity identity)
		{
			return identity != null && (identity.Name == _config.JsonRpcUser && identity.Password == _config.JsonRpcPassword);
		}
	}
}