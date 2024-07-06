namespace mylib;

public static class MyStringExtensions
{
    public static string AddChar(this string str, char c)
    {
        return $"{str}{c}";
    }

    public static string DoSomethingElse(this string str)
    {
        return str.AddChar(' ') ;
    }
}
