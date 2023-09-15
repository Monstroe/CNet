namespace TestServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server(7777);
            server.Start();
        }
    }
}