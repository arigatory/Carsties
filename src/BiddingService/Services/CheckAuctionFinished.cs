﻿namespace BiddingService;
using Contracts;
using MassTransit;
using MongoDB.Entities;

public class CheckAuctionFinished : BackgroundService
{
    private readonly ILogger<CheckAuctionFinished> _logger;
    private readonly IServiceProvider _services;

    public CheckAuctionFinished(ILogger<CheckAuctionFinished> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        _logger.LogInformation("Starting check for finished auctions");

        stoppingToken.Register(() => _logger.LogInformation("==> Auction check is stopping"));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {

                await CheckAuctions(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Что-то плохое случилось при попытке проверить аукционы");
#pragma warning disable CA2254 // Template should be a static expression
                _logger.LogInformation(ex.Message);
#pragma warning restore CA2254 // Template should be a static expression

            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task CheckAuctions(CancellationToken stoppingToken)
    {
        System.Console.WriteLine("1" + "*******!!!");

        var finishedAuctions = await DB.Find<Auction>()
            .Match(x => x.AuctionEnd <= DateTime.UtcNow)
            .Match(x => !x.Finished)
            .ExecuteAsync(stoppingToken);
        System.Console.WriteLine("2" + "*******!!!");

        if (finishedAuctions.Count == 0)
            return;
        System.Console.WriteLine("3" + "*******!!!");

        _logger.LogInformation("==> Found {count} auctions that have completed", finishedAuctions.Count);

        using var scope = _services.CreateScope();
        var endpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        foreach (var auction in finishedAuctions)
        {
            auction.Finished = true;
            await auction.SaveAsync(null, stoppingToken);

            var winningBid = await DB.Find<Bid>()
                .Match(a => a.AuctionId == auction.ID)
                .Match(b => b.BidStatus == BidStatus.Accepted)
                .Sort(x => x.Descending(s => s.Amount))
                .ExecuteFirstAsync(stoppingToken);

            await endpoint.Publish(new AuctionFinished
            {
                ItemSold = winningBid != null,
                AuctionId = auction.ID,
                Winner = winningBid?.Bidder,
                Amount = winningBid?.Amount,
                Seller = auction.Seller
            }, stoppingToken);
        }
    }
}
