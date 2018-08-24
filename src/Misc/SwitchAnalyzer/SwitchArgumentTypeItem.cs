namespace SwitchAnalyzer
{
    internal class SwitchArgumentTypeItem<T>
    {
        public SwitchArgumentTypeItem(string prefix, string member, string fullName, T value)
        {
            Prefix = prefix;
            FullName = fullName;
            Value = value;
            Member = member;
        }

        public string Prefix { get; }

        public string Member { get; }

        public string FullName { get; }

        public T Value { get; set; }
    }
}
