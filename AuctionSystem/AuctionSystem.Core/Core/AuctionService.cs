using AuctionSystem.Core.Models.Auction;
using Grpc.Core;
using Newtonsoft.Json;

namespace AuctionSystem.Core.Core
{
    public class AuctionService : Auction.AuctionBase
    {
        private string _username;
        private string _host;
        private List<AuctionItem> _auctions = new List<AuctionItem>();
        private Dictionary<string, Auction.AuctionClient> _clients = new Dictionary<string, Auction.AuctionClient>();
        private Dictionary<string, string> _hosts = new Dictionary<string, string>();

        public override Task<MessageResponse> SetSettings(SettingsRequest request, ServerCallContext context)
        {
            _username = request.Username;
            _host = request.Host;

            return Task.FromResult(new MessageResponse() { });
        }

        public override Task<AuctionItemResponse> CreateAuction(AuctionItemRequest request, ServerCallContext context)
        {
            if (request != null && !string.IsNullOrEmpty(request.Name) && request.Amount > 0)
            {
                AuctionItem newAuction = new AuctionItem()
                {
                    Name = request.Name,
                    Amount = request.Amount,
                    CurrentBid = request.Amount,
                    Winner = null
                };

                _auctions.Add(newAuction);

                BroadcastMessage($"{_username} opens auction: sell {newAuction.Name} for {newAuction.Amount} USDt.");

                return Task.FromResult(new AuctionItemResponse { Success = true });
            }
            else
            {
                return Task.FromResult(new AuctionItemResponse { Success = false });
            }
        }

        public override Task<BidResponse> PlaceBid(BidRequest request, ServerCallContext context)
        {
            var auction = _auctions.Where(x => x.Name.ToLower() == request.AuctionName.ToLower()).FirstOrDefault();

            if (auction != null)
            {
                if (request.BidAmount > auction.CurrentBid)
                {
                    auction.CurrentBid = request.BidAmount;
                    auction.Winner = request.Username;

                    Console.WriteLine($"Bid placed by {request.Username} for {request.BidAmount} USDt.");

                    return Task.FromResult(new BidResponse { Success = true });
                }
                else
                {
                    Console.WriteLine($"Bid rejected for {request.BidAmount}. Current highest bid is {auction.CurrentBid} USDt.");

                    return Task.FromResult(new BidResponse { Success = false, Message = "Bid amount too low!" });
                }
            }

            return Task.FromResult(new BidResponse { Success = false, Message = "Auction not found." });
        }

        public override Task<MessageResponse> BroadcastMessage(MessageRequest request, ServerCallContext context)
        {
            return Task.FromResult(new MessageResponse { Message = "" });
        }

        public override async Task Connect(ConnectRequest request, IServerStreamWriter<MessageResponse> responseStream, ServerCallContext context)
        {
            Channel channel = new Channel(request.ClientHost, ChannelCredentials.Insecure);
            var client = new Auction.AuctionClient(channel);

            var addClientResponse = client.AddClient(new AddClientRequest() { Username = request.MyUsername, Host = request.MyHost });

            if (addClientResponse.Username != _username)
            {
                _clients.Add(addClientResponse.Username, client);
                _hosts.Add(addClientResponse.Username, request.ClientHost);
            }

            var hosts = addClientResponse.Hosts.Split(';').ToList();

            foreach (var host in hosts)
            {
                if (!string.IsNullOrEmpty(host) && host != _host && !_hosts.Values.Contains(host))
                {
                    Channel hostChannel = new Channel(host, ChannelCredentials.Insecure);
                    var hostClient = new Auction.AuctionClient(hostChannel);

                    var addHostClientResponse = hostClient.AddClient(new AddClientRequest() { Username = request.MyUsername, Host = request.MyHost });

                    if (addHostClientResponse.Username != _username)
                    {
                        _clients.Add(addHostClientResponse.Username, hostClient);
                        _hosts.Add(addHostClientResponse.Username, host);
                    }
                }
            }
        }

        public override Task<AddClientResponse> AddClient(AddClientRequest request, ServerCallContext context)
        {
            Channel channel = new Channel(request.Host, ChannelCredentials.Insecure);
            var client = new Auction.AuctionClient(channel);

            _clients.Add(request.Username, client);
            _hosts.Add(request.Username, request.Host);

            return Task.FromResult(new AddClientResponse { Username = _username, Hosts = string.Join(';', _hosts.Values) });
        }

        public override Task<MessageResponse> ListClients(MessageRequest request, ServerCallContext context)
        {
            return Task.FromResult(new MessageResponse { Message = string.Join(';', _clients.Keys) });
        }

        public override Task<MessageResponse> SendMessage(MessageRequest request, ServerCallContext context)
        {
            Console.WriteLine(request.Message);

            return Task.FromResult(new MessageResponse { Message = "Ok" });
        }

        public override Task<MessageResponse> SendBid(SendBidRequest request, ServerCallContext context)
        {
            if (!_clients.ContainsKey(request.Username))
            {
                return Task.FromResult(new MessageResponse { Message = "Unknown username." });
            }

            var placeBidResponse = _clients[request.Username].PlaceBid(new BidRequest() { AuctionName = request.AuctionName, BidAmount = request.BidAmount, Username = request.BidUsername });

            if (placeBidResponse.Success)
            {
                BroadcastMessage($"{request.BidUsername} bids {request.BidAmount} USDt for {request.Username}'s {request.AuctionName}.");
            }
            else
            {
                Console.WriteLine(placeBidResponse.Message);
            }

            return Task.FromResult(new MessageResponse { Message = "Ok" });
        }

        public override Task<MessageResponse> CloseAuction(CloseAuctionRequest request, ServerCallContext context)
        {
            var auction = _auctions.Where(x => x.Name.ToLower() == request.AuctionName.ToLower()).FirstOrDefault();

            if (auction != null)
            {
                if (auction.Winner != null)
                {
                    BroadcastMessage($"{_username} finalizes auction {request.AuctionName}, informing all about the sale to {auction.Winner} at {auction.CurrentBid} USDt.");
                }
                else
                {
                    BroadcastMessage($"{_username} finalizes auction {request.AuctionName}, no placed bids.");
                }

                _auctions.Remove(auction);

                return Task.FromResult(new MessageResponse { Message = "Ok" });
            }

            Console.WriteLine("Auction not found.");

            return Task.FromResult(new MessageResponse { Message = "Auction not found." });
        }

        public override Task<MessageResponse> GetAllAuctions(GetAuctionsRequest request, ServerCallContext context)
        {
            Console.WriteLine($"### Auctions of {_username} ###");

            foreach (var auction in _auctions)
            {
                Console.WriteLine($"### Name: '{auction.Name}' Current Bid: '{auction.CurrentBid}' Current Winner: '{auction.Winner}'");
            }

            foreach (var clientKey in _clients.Keys)
            {
                var auctionsResponse = _clients[clientKey].GetClientAuctions(new GetAuctionsRequest() { });
                List<AuctionItem> auctions = JsonConvert.DeserializeObject<List<AuctionItem>>(auctionsResponse.Auctions);

                Console.WriteLine($"### Auctions of {auctionsResponse.Username} ###");

                foreach (var auction in auctions)
                {
                    Console.WriteLine($"### Name: '{auction.Name}' Current Bid: '{auction.CurrentBid}' Current Winner: '{auction.Winner}'");
                }
            }

            return Task.FromResult(new MessageResponse());
        }

        public override Task<GetAuctionsResponse> GetClientAuctions(GetAuctionsRequest request, ServerCallContext context)
        {
            GetAuctionsResponse result = new GetAuctionsResponse()
            {
                Username = _username,
                Auctions = JsonConvert.SerializeObject(_auctions)
            };

            return Task.FromResult(result);
        }

        private void BroadcastMessage(string message)
        {
            foreach (var clientKey in _clients.Keys)
            {
                _clients[clientKey].SendMessage(new MessageRequest() { Message = message });
            }
        }
    }
}
