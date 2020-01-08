using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Rpc
{
    public class JsonRpcServer
    {
        private long _running;
        public bool IsRunning => Interlocked.Read(ref _running) == 1;
        public bool IsStopping => Interlocked.Read(ref _running) == 2;
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

        public void Start()
        {
            Interlocked.Exchange(ref _running, 1);
            Listener.Start();

            Task.Factory.StartNew(async _ =>
            {
                try
                {
                    var handler = new JsonRpcRequestHandler<WasabiJsonRpcService>(Service);

                    while (IsRunning)
                    {
                        var context = Listener.GetContext();
                        var request = context.Request;
                        var response = context.Response;

                        if (request.HttpMethod == "POST")
                        {
                            using var reader = new StreamReader(request.InputStream);
                            string body = await reader.ReadToEndAsync();

                            var identity = (HttpListenerBasicIdentity)context.User?.Identity;
                            if (!Config.RequiresCredentials || CheckValidCredentials(identity))
                            {
                                var result = await handler.HandleAsync(body, Cancellation.Token);

                                // result is null only when the request is a notification.
                                if (!string.IsNullOrEmpty(result))
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
            }, Cancellation.Token, TaskCreationOptions.LongRunning);
        }

        internal void Stop()
        {
            Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.;
            Listener.Stop();
            Cancellation.Cancel();
            while (IsStopping)
            {
                Task.Delay(50).GetAwaiter().GetResult();
            }
            Cancellation.Dispose();
        }

        private bool CheckValidCredentials(HttpListenerBasicIdentity identity)
        {
            return identity is { } && (identity.Name == Config.JsonRpcUser && identity.Password == Config.JsonRpcPassword);
        }
    }
}
