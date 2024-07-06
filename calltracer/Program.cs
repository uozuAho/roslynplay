namespace roslynplay
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CallTracer.RunFromArgs(args);
        }
    }
}
