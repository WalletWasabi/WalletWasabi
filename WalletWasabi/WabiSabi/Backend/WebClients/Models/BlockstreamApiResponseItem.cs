using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Backend.WebClients.Models;

public class BlockstreamApiResponseItem
{
	public string txid { get; set; }
	public int version { get; set; }
	public int locktime { get; set; }
	public List<Vin> vin { get; set; }
	public List<Vout> vout { get; set; }
	public int size { get; set; }
	public int weight { get; set; }
	public int fee { get; set; }
	public Status status { get; set; }
	
	public record Prevout
	{
		public string scriptpubkey { get; set; }
		public string scriptpubkey_asm { get; set; }
		public string scriptpubkey_type { get; set; }
		public string scriptpubkey_address { get; set; }
		public int value { get; set; }
	}

	public record Status
	{
		public bool confirmed { get; set; }
		public int block_height { get; set; }
		public string block_hash { get; set; }
		public int block_time { get; set; }
	}

	public record Vin
	{
		public string txid { get; set; }
		public int vout { get; set; }
		public Prevout prevout { get; set; }
		public string scriptsig { get; set; }
		public string scriptsig_asm { get; set; }
		public List<string> witness { get; set; }
		public bool is_coinbase { get; set; }
		public object sequence { get; set; }
	}

	public record Vout
	{
		public string scriptpubkey { get; set; }
		public string scriptpubkey_asm { get; set; }
		public string scriptpubkey_type { get; set; }
		public string scriptpubkey_address { get; set; }
		public int value { get; set; }
	}
}