namespace Gma.QrCodeNet.Encoding.DataEncodation
{
    public enum Mode
    {
        Numeric = 0001,
        Alphanumeric = 0001 << 1,
        EightBitByte = 0001 << 2,
        Kanji = 0001 << 3,
    }
}
