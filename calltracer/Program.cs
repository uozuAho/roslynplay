namespace roslynplay
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args[0] == "trace")
                await CallTracer.RunFromArgs(args[1..]);
            else if (args[0] == "find")
                await Finder.RunFromArgs(args[1..]);
        }
    }
}
