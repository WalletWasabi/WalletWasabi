using NBitcoin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Models.ChaumianCoinJoin
{
	/// <summary>
	/// Represents the DoS protection parameters used by the coordinator
	/// to enforce the DoS protection policy.
	/// This info is extracted from the CcjRoundConfig instance to isolate
	/// and encapsulate the info that is specific to the DoS protection mechanism. 
	/// </summary>
	public class CcjDenialOfServiceConfig
	{
		public int Severity { get; internal set; }
		public long DurationHours { get; internal set; }
		public bool NoteBeforeBan { get; internal set; }

		public CcjDenialOfServiceConfig(int severity, long durationHours, bool noteBeforeBan)
		{
			Severity = severity;
			DurationHours = durationHours;
			NoteBeforeBan = noteBeforeBan;
		}

		/// <summary>
		/// Extracts the DoS policy config items from a CcjRoundConfig instance.
		/// </summary>
		public static CcjDenialOfServiceConfig FromCcjRoundConfig(CcjRoundConfig cfg)
		{
			return new CcjDenialOfServiceConfig(cfg.DosSeverity ?? 1, cfg.DosDurationHours ?? 24, cfg.DosNoteBeforeBan ?? true);
		}
	}
}
