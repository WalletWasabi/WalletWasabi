using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.Backend.Models.Requests
{
	public class WrappedBlindSignature
	{
		[Required]
		public string C { get; set; }

		[Required]
		public string S { get; set; }

		public BlindSignature Unwrap()
		{
			return new BlindSignature(new BigInteger(C), new BigInteger(S));
		}

		public static WrappedBlindSignature Wrap(BlindSignature blindSignature)
		{
			return new WrappedBlindSignature { C = blindSignature.C.ToString(), S = blindSignature.S.ToString() };
		} 
	} 
}
