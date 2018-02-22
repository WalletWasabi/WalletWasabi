namespace NBitcoin
{
	public static class FastBitArrayExtensions
	{
		public static byte[] ToByteArray(this FastBitArray me)
		{
			var byteCount = me.Length==0 ? 0 : (me.Length-1)/ 8 + 1;
			var bytes = new byte[byteCount];
			me.CopyTo(bytes, 0);
			return bytes;
		}
	}
}