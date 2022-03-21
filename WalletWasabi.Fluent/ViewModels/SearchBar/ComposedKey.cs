using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.SearchBar
{
    public class ComposedKey : ValueObject
    {
        public object[] Keys { get; }

        public ComposedKey(params object[] keys)
        {
            Keys = keys;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            return Keys;
        }
    }
}