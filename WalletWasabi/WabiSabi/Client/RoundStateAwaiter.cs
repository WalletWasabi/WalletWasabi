using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public record RoundStateAwaiter(TaskCompletionSource<RoundState> Task, Predicate<RoundState> Predicate)
	{
	}
}
