using HiddenWallet.WebClients.BlockCypher;
using NBitcoin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.FullSpv.Fees
{
    public class FeeService : IDisposable
    {
        private FeeRate _lowFee = null;
        private FeeRate _mediumFee = null;
        private FeeRate _highFee = null;

        public Network Network { get; }
        private BlockCypherClient BlockCypherClient { get; }
        private SemaphoreSlim Semaphore { get; }
        private DotNetTor.ControlPort.Client TorControlPortClient { get; }
        private bool DisposeTorControl { get; }

        public FeeService(Network network, DotNetTor.ControlPort.Client torControlPortClient = null, bool disposeTorControl = false, HttpMessageHandler handler = null, bool disposeHandler = false)
        {
            TorControlPortClient = torControlPortClient;
            DisposeTorControl = disposeTorControl;
            Network = network ?? throw new ArgumentNullException(nameof(network));
            Semaphore = new SemaphoreSlim(1, 1); // don't make async requests, linux and mac can fail for no reason
            if (network == Network.Main)
            {
                BlockCypherClient = new BlockCypherClient(Network.Main, handler, disposeHandler);
            }
            else
            {
                // BlockCypher doesn't support anything else
                BlockCypherClient = new BlockCypherClient(Network.TestNet, handler, disposeHandler);
            }
        }

        public async Task<FeeRate> GetFeeRateAsync(FeeType feeType, CancellationToken ctsToken)
        {
            while (_firstRun)
            {
                ctsToken.ThrowIfCancellationRequested();
                await Task.Delay(100, ctsToken).ContinueWith(t => { });
            }
            ctsToken.ThrowIfCancellationRequested();

            switch (feeType)
            {
                case FeeType.Low: return _lowFee;
                case FeeType.Medium: return _mediumFee;
                case FeeType.High: return _highFee;
                default: throw new ArgumentException(nameof(feeType));
            }
        }

        private bool _firstRun = true;
        public async Task StartAsync(CancellationToken ctsToken)
        {
            while (true)
            {
                try
                {
                    await Semaphore.WaitAsync(ctsToken);
                    if (ctsToken.IsCancellationRequested) return;

                    if (TorControlPortClient != null)
                    {
                        await TorControlPortClient.ChangeCircuitAsync(ctsToken);
                    }

                    var generalInfo = await BlockCypherClient.GetGeneralInformationAsync(ctsToken);

                    _lowFee = generalInfo.LowFee;
                    _mediumFee = generalInfo.MediumFee;
                    _highFee = generalInfo.HighFee;

                    if (ctsToken.IsCancellationRequested) return;
                    _firstRun = false;
                }
                catch (OperationCanceledException)
                {
                    if (ctsToken.IsCancellationRequested) return;
                    continue;
                }
                catch (Exception ex)
                {
                    if (_firstRun) throw;

                    Debug.WriteLine($"Ignoring {nameof(FeeService)} exception:");
                    Debug.WriteLine(ex);
                }
                finally
                {
                    Semaphore.SafeRelease();
                }

                var waitMinutes = new Random().Next(3, 10);
                await Task.Delay(TimeSpan.FromMinutes(waitMinutes), ctsToken).ContinueWith(t => { });
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                    BlockCypherClient?.Dispose();
                    Semaphore?.Dispose();
                    if (DisposeTorControl)
                    {
                        TorControlPortClient?.DisconnectDisposeSocket();
                    }
                }

                // free unmanaged resources (unmanaged objects) and override a finalizer below.
                // set large fields to null.

                disposedValue = true;
            }
        }

        // override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~BlockCypherClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
