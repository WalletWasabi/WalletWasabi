namespace WalletWasabi.Fluent.Helpers
{
    /// <summary>
    /// A stack that retains only the last items it can store
    /// and removes the older entries automatically.
    /// </summary>
    public class DropOutStack<T>
    {
        private int _top = 0;
        private readonly int _capacity;

        public DropOutStack(int capacity)
        {
            _capacity = capacity;
            Items = new T[_capacity];
        }

        public void Push(T item)
        {
            Items[_top] = item;
            _top = (_top + 1) % Items.Length;
        }

        public T Pop()
        {
            _top = (Items.Length + _top - 1) % Items.Length;
            return Items[_top];
        }

        public T[] Items { get; private set; }

        public void Clear()
        {
            _top = 0;
            Items = new T[_capacity];
        }
    }
}