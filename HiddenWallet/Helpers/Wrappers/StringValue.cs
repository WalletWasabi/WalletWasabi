// http://stackoverflow.com/a/479419/2061103

namespace HiddenWallet.Helpers.Wrappers
{
    internal class StringValue
    {
        public StringValue(string s)
        {
            Value = s;
        }

        public string Value { get; set; }
    }
}