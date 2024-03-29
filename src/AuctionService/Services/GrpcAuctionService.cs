namespace AuctionService.Services;

using System.Globalization;
using AuctionService.Data;
using Grpc.Core;

public class GrpcAuctionService : GrpcAuction.GrpcAuctionBase
{
    private readonly AuctionDbContext _dbContext;

    public GrpcAuctionService(AuctionDbContext dbContext) => _dbContext = dbContext;

    public override async Task<GrpcAuctionResponse> GetAuction(GetAuctionRequest request, ServerCallContext context)
    {
        Console.WriteLine("==> Reveived Grpc request for auction");

        var auction = await _dbContext.Auctions.FindAsync(Guid.Parse(request.Id)) ?? throw new RpcException(new Status(StatusCode.NotFound, "Not found"));

        var response = new GrpcAuctionResponse
        {
            Auction = new GrpcAuctionModel
            {
                AuctionEnd = auction.AuctionEnd.ToString(CultureInfo.InvariantCulture),
                Id = auction.Id.ToString(),
                ReservePrice = auction.ReservePrice,
                Seller = auction.Seller
            }
        };

        return response;
    }
}
