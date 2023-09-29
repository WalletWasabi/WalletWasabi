using System.Collections.Generic;


namespace WalletWasabi.WebClients.MempoolSpace;
public class MempoolSpaceApiResponseItem
{
	public required string Txid { get; set; }
	public int Version { get; set; }
	public int Locktime { get; set; }
	public List<Vin> Vin_List { get; set; }
	public List<Vout> Vout_List { get; set; }
	public int Size { get; set; }
	public int Weight { get; set; }
	public int Fee { get; set; }
	public Status TxStatus { get; set; }

	public record Prevout
	{
		public string Scriptpubkey { get; set; }
		public string Scriptpubkey_asm { get; set; }
		public string Scriptpubkey_type { get; set; }
		public string Scriptpubkey_address { get; set; }
		public int Value { get; set; }
	}

	public record Status
	{
		public bool Confirmed { get; set; }
		public int Block_height { get; set; }
		public string Block_hash { get; set; }
		public int Block_time { get; set; }
	}

	public record Vin
	{
		public string Txid { get; set; }
		public int Vout { get; set; }
		public Prevout Prevout { get; set; }
		public string Scriptsig { get; set; }
		public string Scriptsig_asm { get; set; }
		public List<string> Witness { get; set; }
		public bool Is_coinbase { get; set; }
		public object Sequence { get; set; }
	}

	public record Vout
	{
		public string Scriptpubkey { get; set; }
		public string Scriptpubkey_asm { get; set; }
		public string Scriptpubkey_type { get; set; }
		public string Scriptpubkey_address { get; set; }
		public int Value { get; set; }
	}
}
