using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;

namespace WalletWasabi.TorSocks5
{
	public enum TorProcessState
	{
		NotStarted,
		Running,
		Stopped,
		Failed
	}

	internal class TorProcessManager
	{
		public readonly static TorProcessManager Default = new TorProcessManager();
		private readonly static IPEndPoint DefaultSocksEndpoint = new IPEndPoint(IPAddress.Loopback, DefaultSocksPort);

		public const int DefaultSocksPort = 9050;
		public const string TorInstallingFolder = "tor";

		private Process _torProcess;
		private IPEndPoint _torEndPoint; 

		public int SocksPort { get; }
		public string TorPath { get; }
		public TorProcessState Status { get; private set; }
		
		public bool IsManaged => _torProcess != null && !_torProcess.HasExited;

		public EventHandler<TorProcessStatusEventArgs> TorProcessStatusChanged; 

		public TorProcessManager()
			: this(DefaultSocksPort, TorInstallingFolder)
		{
		}

		public TorProcessManager(int socksPort, string torPath)
		{
			SocksPort = socksPort;
			TorPath = torPath;
			_torEndPoint = new IPEndPoint(IPAddress.Loopback, SocksPort);
			ChangeStatus(TorProcessState.NotStarted);
		}

		public void Start()
		{
			if(Status == TorProcessState.Running) return;

			Logging.Logger.LogInfo<TorProcessManager>("Starting Tor process");
			try
			{
				var arguments = $"SOCKSPort {SocksPort}";
				var processStartInfo = new ProcessStartInfo(TorPath)
				{
					Arguments = arguments,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true
				};
				_torProcess = Process.Start(processStartInfo);
				ChangeStatus(TorProcessState.Running);
			}
			catch(Exception e)
			{
				ChangeStatus(TorProcessState.Failed);
				Logging.Logger.LogError<TorProcessManager>("Starting Tor process Failed.");
				Logging.Logger.LogError<TorProcessManager>(e.ToString());
			}
		}

		public void Stop()
		{
			if(Status != TorProcessState.Running) return;

			if (IsManaged)
			{
				Logging.Logger.LogInfo<TorProcessManager>("Stopping Tor process");
				using(_torProcess){
					_torProcess.Kill();
					_torProcess.WaitForExit();
					_torProcess = null;
				}
				ChangeStatus(TorProcessState.Stopped);
			}
		}

		public async Task<bool> IsRunningAsync()
		{
			if(!IsManaged) 
				return false;

			return await IsTorRunningAsync(_torEndPoint);
		}

		public static async Task<bool> IsTorRunningAsync(IPEndPoint torSocks5EndPoint = null )
		{
			torSocks5EndPoint = torSocks5EndPoint ?? DefaultSocksEndpoint;
			using (var client = new TorSocks5Client(torSocks5EndPoint))
			{
				try
				{
					await client.ConnectAsync();
					await client.HandshakeAsync();
				}
				catch (ConnectionException)
				{
					return false;
				}
				return true;
			}
		}

		private void ChangeStatus(TorProcessState curStatus)
		{
			var prevStatus = Status; 
			Status = curStatus;

			if(prevStatus != curStatus)
				TorProcessStatusChanged?.Invoke(this, new TorProcessStatusEventArgs(prevStatus, curStatus));
		}
	}
}
