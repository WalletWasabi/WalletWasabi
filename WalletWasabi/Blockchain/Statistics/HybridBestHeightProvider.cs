using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Blockchain.Statistics
{
	/// <summary>
	/// Manages multiple best height sources. Returns the best one.
	/// Prefers local full node, as long as it's synchronized.
	/// </summary>
	public class HybridBestHeightProvider : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		public HybridBestHeightProvider(WasabiSynchronizer synchronizer, RpcMonitor? rpcMonitor)
		{
			Synchronizer = synchronizer;
			RpcMonitor = rpcMonitor;

			Synchronizer.ResponseArrived += Synchronizer_ResponseArrived;

			if (RpcMonitor is not null)
			{
				RpcMonitor.RpcStatusChanged += RpcMonitor_RpcStatusChanged;
			}

			var rpcStatus = RpcMonitor?.RpcStatus;
			if (rpcStatus is not null && rpcStatus.Success)
			{
				SetBestHeightIfLooksBetter((int)rpcStatus.Headers);
			}
			SetBestHeightIfLooksBetter(Synchronizer.LastResponse?.BestHeight);
		}

		public event EventHandler<int>? BestHeightChanged;

		public WasabiSynchronizer Synchronizer { get; }
		public RpcMonitor? RpcMonitor { get; }
		private object Lock { get; } = new object();
		public int? BestHeight { get; private set; }

		private void Synchronizer_ResponseArrived(object? sender, SynchronizeResponse response)
		{
			OnBestHeightArrived(sender, response.BestHeight);
		}

		private void RpcMonitor_RpcStatusChanged(object? sender, RpcStatus status)
		{
			OnBestHeightArrived(sender, (int)status.Headers);
		}

		private void OnBestHeightArrived(object? sender, int height)
		{
			var notify = false;
			lock (Lock)
			{
				if (BestHeight is null)
				{
					// If it wasn't set before, then set it regardless everything.
					notify = SetBestHeight(height);
				}
				else if (sender is WasabiSynchronizer)
				{
					if (RpcMonitor is null)
					{
						// If user doesn't use full node, then set it, this is the best we got.
						notify = SetBestHeight(height);
					}
					else
					{
						if (RpcMonitor.RpcStatus.Success)
						{
							// If user's full node is properly serving data, then we don't care about the backend.
							return;
						}
						else
						{
							if (Synchronizer.BackendStatus == BackendStatus.Connected)
							{
								// If the backend is properly serving accurate data then, this is the best we got.
								notify = SetBestHeight(height);
							}
							else
							{
								// If neither user's full node, nor backend is ready, then let's try our best effort figuring out which data looks better:
								notify = SetBestHeightIfLooksBetter(height);
							}
						}
					}
				}
				else if (sender is RpcMonitor rpcMonitor)
				{
					if (rpcMonitor.RpcStatus.Success)
					{
						// If user's full node is properly serving data, we're done here.
						notify = SetBestHeight(height);
					}
					else
					{
						if (Synchronizer.BackendStatus == BackendStatus.Connected)
						{
							// If the user's full node isn't ready, but the backend is, then let's leave it to the backend.
							return;
						}
						else
						{
							// If neither user's full node, nor backend is ready, then let's try our best effort figuring out which data looks better:
							notify = SetBestHeightIfLooksBetter(height);
						}
					}
				}
				//else if (sender is WasabiSynchronizer && RpcMonitor is null)
				//{
				//	// If it is coming from the the backend and user doesn't use a full node, then set the fees.
				//	notify = SetBestHeight(height);
				//}
				//else if (sender is WasabiSynchronizer && RpcMonitor?.InError is true)
				//{
				//	// If data is coming from the the backend, user uses a full node, but it doesn't provide data, then set it.
				//	notify = SetBestHeight(height);
				//}
				//else if (sender is WasabiSynchronizer && RpcMonitor?.IsSynchronized is false)
				//{
				//	// If data is coming from the the backend, user uses a full node, but it isn't synchronized yet, then set it.
				//	notify = SetBestHeight(height);
				//}
				//else if (sender is RpcMonitor && RpcMonitor.IsSynchronized is true)
				//{
				//	// If our RPC is synchronized, then it probably has its height reight.
				//	notify = SetBestHeight(height);
				//}
				//else if (sender is RpcMonitor && Synchronizer.BackendStatus != BackendStatus.Connected)
				//{
				//	// If backend isn't connected, then even if non synchrnonized data comes from RPC, let's believe it.
				//	notify = SetBestHeight(height);
				//}
			}

			if (notify)
			{
				Logger.LogInfo($"Best blockchain height is acquired from {sender?.GetType()?.Name}: {height}.");
				BestHeightChanged?.Invoke(this, height);
			}
		}

		/// <returns>True if changed.</returns>
		private bool SetBestHeightIfLooksBetter(int? height)
		{
			var current = BestHeight;
			if (height is null || height <= current)
			{
				return false;
			}
			return SetBestHeight(height.Value);
		}

		/// <returns>True if changed.</returns>
		private bool SetBestHeight(int height)
		{
			if (BestHeight == height)
			{
				return false;
			}
			BestHeight = height;
			return true;
		}

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Synchronizer.ResponseArrived -= Synchronizer_ResponseArrived;

					if (RpcMonitor is not null)
					{
						RpcMonitor.RpcStatusChanged -= RpcMonitor_RpcStatusChanged;
					}
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
