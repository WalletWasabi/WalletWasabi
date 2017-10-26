using HiddenWallet.ChaumianTumbler.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
	//Sealed to prevent inherentance and breaking of Singleton design pattern
	public sealed class ChaumianTumbler
    {
		// Singleton instance
		// IMPORTANT:   Lazy initialization is used for the _instance field, not for performance 
		//              reasons but to ensure that the instance creation is threadsafe.
		//              Lazy initialization is thread-safe, but it doesn't protect everything  
		//              within the class after creation - i.e. you must lock things held in it 
		//              like collections etc. before accessing them.
		private readonly static Lazy<ChaumianTumbler> _instance = new Lazy<ChaumianTumbler>(() => new ChaumianTumbler());

		private IHubContext<ChaumianTumblerHub> _context; //The context of the hub - needed in order for the tumbler to act on MVC submitted data and call the hub to issue updates

		//	In the initial check in we will use an example collection to hold requests and respond via the hub. 
		//	To be changed later in development when processing of inputs etc are added.
		private readonly ConcurrentDictionary<string, InputsRequest> _InputsRequests = new ConcurrentDictionary<string, InputsRequest>();
		private readonly object _inputsRequestsLock = new object();
		private volatile bool _inputsRequestsUpdating = false;

		private ChaumianTumbler()
		{
			//	Put any code to initliase collections etc. here.
		}

		public static ChaumianTumbler Instance
		{
			get
			{
				return _instance.Value;
			}
		}

		public IHubContext<ChaumianTumblerHub> ChatHub
		{
			set
			{
				_context = value;
			}
		}

		//	An example method that will be called from the MVC code - for example when adding inputs for processing
		public void ProcessInputsRequest(InputsRequest request)
		{
			//	ConcurrentDictionary provides better performance than lock + Dictionary for read heavy and read/update 
			//	actions. However - locks around the ConcurrentDictionary are still needed as individual operations 
			//	may be thread-safe, but sequences of operations are not atomic - for example check .Count then .Contains
			lock (_inputsRequestsLock)
			{
				if (!_inputsRequestsUpdating)
				{
					_inputsRequestsUpdating = true;

					//TODO - what can we use as a uniqe ID? Maybe ditch and use array - depends on how much reading we need to do.
					_InputsRequests.TryAdd(request.BlindedOutput, request);

					//If some condition is met (e.g. we have had all inputs required and we are ready for output registration):
					if (_InputsRequests.Count() >= 3)
					{
						string exampleExtraData = DateTime.Now.ToString();

						PhaseChangeBroadcast broadcast = new PhaseChangeBroadcast { NewPhase = TumblerPhase.InputConfirmation, Message = exampleExtraData };
						
						BroadcastPhaseChange(broadcast);
					}

					_inputsRequestsUpdating = false;
				}
			}
		}

		private void BroadcastPhaseChange(PhaseChangeBroadcast broadcast)
		{
			IClientProxy proxy = _context.Clients.All;
			string json = JsonConvert.SerializeObject(broadcast);
			proxy.InvokeAsync("phaseChange", json);
		}
	}
}
