using NBitcoin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HBitcoin.Fees
{
    public class FeeJob
	{
		private Money _lowFeePerBytes = null;
		private Money _mediumFeePerBytes = null;
		private Money _highFeePerBytes = null;

		private DotNetTor.ControlPort.Client _controlPortClient;
		private HttpClient _torHttpClient;
		public FeeJob(DotNetTor.ControlPort.Client controlPort, HttpClient torHttpClient)
		{
			_controlPortClient = controlPort;
			_torHttpClient = torHttpClient;
		}

		public async Task<Money> GetFeePerBytesAsync(FeeType feeType, CancellationToken ctsToken)
		{
			while (_firstRun)
			{
				ctsToken.ThrowIfCancellationRequested();
				await Task.Delay(100, ctsToken).ContinueWith(t => { }).ConfigureAwait(false);
			}
			ctsToken.ThrowIfCancellationRequested();

			switch (feeType)
			{
				case FeeType.Low: return _lowFeePerBytes;
				case FeeType.Medium: return _mediumFeePerBytes;
				case FeeType.High: return _highFeePerBytes;
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
					if (ctsToken.IsCancellationRequested) return;

					await _controlPortClient.ChangeCircuitAsync(ctsToken).ConfigureAwait(false);

					HttpResponseMessage response =
						await _torHttpClient.GetAsync(@"http://api.blockcypher.com/v1/btc/main", HttpCompletionOption.ResponseContentRead, ctsToken)
						.ConfigureAwait(false);

					var json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
					_lowFeePerBytes = new Money((int)(json.Value<decimal>("low_fee_per_kb") / 1024), MoneyUnit.Satoshi);
					_mediumFeePerBytes = new Money((int)(json.Value<decimal>("medium_fee_per_kb") / 1024), MoneyUnit.Satoshi);
					_highFeePerBytes = new Money((int)(json.Value<decimal>("high_fee_per_kb") / 1024), MoneyUnit.Satoshi);

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

					Debug.WriteLine($"Ignoring {nameof(FeeJob)} exception:");
					Debug.WriteLine(ex);
				}

				var waitMinutes = new Random().Next(3, 10);
				await Task.Delay(TimeSpan.FromMinutes(waitMinutes), ctsToken).ContinueWith(t => { }).ConfigureAwait(false);
			}
		}
	}
}
