using AuctionSystem.Core.Core;
using Grpc.Core;

class Program
{
    private static Auction.AuctionClient _client;
    private static string _username;
    private static int _port;

    public static void Main(string[] args)
    {
        _username = args[0];
        _port = int.Parse(args[1]);

        ConsoleKeyInfo keyinfo;

        Server server = new Server
        {
            Services = { Auction.BindService(new AuctionService()) },
            Ports = { new ServerPort("localhost", _port, ServerCredentials.Insecure) }
        };

        server.Start();

        Channel channel = new Channel($"localhost:{_port}", ChannelCredentials.Insecure);
        _client = new Auction.AuctionClient(channel);
        _client.SetSettings(new SettingsRequest() { Username = _username, Host = $"localhost:{_port}" });

        do
        {
            Console.WriteLine("#### Auction System ####");
            Console.WriteLine();
            Console.WriteLine("1. Connect");
            Console.WriteLine("2. Clients List");
            Console.WriteLine("3. Create Auction");
            Console.WriteLine("4. Send Bid");
            Console.WriteLine("5. Close Auction");
            Console.WriteLine("6. Get All Auctions");
            Console.WriteLine();
            Console.WriteLine("X. Exit");

            keyinfo = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            switch (keyinfo.Key)
            {
                case ConsoleKey.D1:
                    Connect();
                    break;
                case ConsoleKey.D2:
                    ListClients();
                    break;
                case ConsoleKey.D3:
                    CreateAuction();
                    break;
                case ConsoleKey.D4:
                    SendBid();
                    break;
                case ConsoleKey.D5:
                    CloseAuction();
                    break;
                case ConsoleKey.D6:
                    GetAllAuctions();
                    break;
                case ConsoleKey.X:
                    Console.WriteLine("EXIT");
                    break;
            }
        }
        while (keyinfo.Key != ConsoleKey.X);

        server.ShutdownAsync().Wait();
    }

    private static void Connect()
    {
        Console.Write("Insert 'host:port': ");
        string host = Console.ReadLine();

        _client.Connect(new ConnectRequest() { MyUsername = _username, MyHost = $"localhost:{_port}", ClientHost = host });
    }

    private static void ListClients()
    {
        var clientsResult = _client.ListClients(new MessageRequest());

        Console.WriteLine("Clients: " + clientsResult.Message);
    }

    private static void CreateAuction()
    {
        Console.Write("Insert Auction Name: ");
        string auctionName = Console.ReadLine();
        Console.Write("Insert Auction Amount: ");
        string auctionAmount = Console.ReadLine();

        AuctionItemRequest newAuction = new AuctionItemRequest()
        {
            Name = auctionName,
            Amount = int.Parse(auctionAmount)
        };

        _client.CreateAuction(newAuction);
    }

    private static void SendBid()
    {
        Console.Write("Insert auction owner username: ");
        string username = Console.ReadLine();
        Console.Write("Insert Auction Name: ");
        string auctionName = Console.ReadLine();
        Console.Write("Insert Bid Amount: ");
        string bidAmount = Console.ReadLine();

        _client.SendBid(new SendBidRequest() { Username = username, AuctionName = auctionName, BidAmount = double.Parse(bidAmount), BidUsername = _username });
    }

    private static void CloseAuction()
    {
        Console.Write("Insert Auction Name: ");
        string auctionName = Console.ReadLine();

        _client.CloseAuction(new CloseAuctionRequest() { AuctionName = auctionName });
    }

    private static void GetAllAuctions()
    {
        _client.GetAllAuctions(new GetAuctionsRequest());
    }
}