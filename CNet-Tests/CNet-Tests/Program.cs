namespace CNet_Tests;

class Program
{
    private static bool testsRunning = false;

    static void Main(string[] args)
    {
        string address = "127.0.0.1";
        int port = 7777;
        string connectionKey = "CNetTest";

        List<ClientNetworkTest> clientTests = new List<ClientNetworkTest>()
        {
            new ClientDenyTest(),
            new ClientConnectDisconnectTest(),
            new ClientReceiveTest(),
            new ClientDisconnectWithMessageTest(),
            new ClientServerClosedTest(),
            // ADD MORE CLIENT TESTS HERE
        };

        List<ListenerNetworkTest> listenerTests = new List<ListenerNetworkTest>()
        {
            new ListenerDenyTest(),
            new ListenerConnectDisconnectTest(),
            new ListenerReceiveTest(),
            new ListenerDisconnectWithMessageTest(),
            new ListenerServerClosedTest(),
            // ADD MORE LISTENER TESTS HERE
        };

        Console.WriteLine("Starting CNet Tests...");
        RunTests(clientTests, listenerTests, address, port, connectionKey);

        testsRunning = true;
        while (testsRunning)
        {
            Thread.Sleep(100);
        }

        Console.WriteLine("All tests completed. Press any key to exit.");
        Console.ReadKey();
    }

    private static async void RunTests(List<ClientNetworkTest> clientTests, List<ListenerNetworkTest> listenerTests, string address, int port, string connectionKey)
    {
        int currentTestIndex = 0;

        while (currentTestIndex < clientTests.Count && currentTestIndex < listenerTests.Count)
        {
            var clientCTS = new CancellationTokenSource();
            var listenerCTS = new CancellationTokenSource();

            Console.WriteLine($"Running Test {currentTestIndex + 1}: {listenerTests[currentTestIndex].GetType().Name} & {clientTests[currentTestIndex].GetType().Name}");
            Task<(bool, string?)> serverTask = RunListenerAsync(listenerTests[currentTestIndex], port, connectionKey, listenerCTS);
            Task<(bool, string?)> clientTask = RunClientAsync(clientTests[currentTestIndex], address, port, connectionKey, clientCTS);

            await Task.WhenAll(serverTask, clientTask);
            var (serverSuccess, serverError) = await serverTask;
            var (clientSuccess, clientError) = await clientTask;

            if (serverSuccess && clientSuccess)
            {
                Console.WriteLine($"Test {currentTestIndex + 1} Passed.");
            }
            else
            {
                Console.WriteLine($"Test {currentTestIndex + 1} Failed.");
                if (!serverSuccess)
                {
                    Console.WriteLine($" - Listener Error: {serverError}");
                }
                if (!clientSuccess)
                {
                    Console.WriteLine($" - Client Error: {clientError}");
                }
            }

            clientTests[currentTestIndex].Client?.Dispose();
            listenerTests[currentTestIndex].Listener?.Dispose();
            currentTestIndex++;
            await Task.Delay(1000); // Short delay between tests
        }

        testsRunning = false;
    }

    private static async Task<(bool, string?)> RunListenerAsync(ListenerNetworkTest listenerTest, int port, string connectionKey, CancellationTokenSource token)
    {
        return await Task.Run(async () =>
        {
            try
            {
                listenerTest.Start(port, connectionKey, token);
                while (!token.Token.IsCancellationRequested)
                {
                    listenerTest.Update();
                    await Task.Delay(15, token.Token);
                }
            }
            catch (OperationCanceledException) { }

            return (listenerTest.TestSuccess, listenerTest.TestError);
        }, token.Token);
    }

    private static async Task<(bool, string?)> RunClientAsync(ClientNetworkTest clientTest, string address, int port, string connectionKey, CancellationTokenSource token)
    {
        return await Task.Run(async () =>
        {
            try
            {
                clientTest.Start(address, port, connectionKey, token);
                while (!token.Token.IsCancellationRequested)
                {
                    clientTest.Update();
                    await Task.Delay(15, token.Token);
                }
            }
            catch (OperationCanceledException) { }

            return (clientTest.TestSuccess, clientTest.TestError);
        }, token.Token);
    }
}
