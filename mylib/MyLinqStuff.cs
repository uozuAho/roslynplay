using MoreLinq;

namespace mylib
{
    public static class MyLinqStuff
    {
        public static IEnumerable<T> DoSomething<T>(this IEnumerable<T> items)
        {
            return items.Choose(x => (x != null, x));
        }
    }
}
