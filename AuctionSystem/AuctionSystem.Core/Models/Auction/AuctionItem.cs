namespace AuctionSystem.Core.Models.Auction
{
    public class AuctionItem
    {
        public string Name { get; set; }
        public double Amount { get; set; }
        public double CurrentBid { get; set; }
        public string Winner { get; set; }
    }
}
