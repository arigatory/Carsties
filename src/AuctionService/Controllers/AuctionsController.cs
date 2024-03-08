using AuctionService.Data;
using AuctionService.DTOs;
using AuctionService.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionService;

[ApiController]
[Route("api/auctions")]
public class AuctionsController : ControllerBase
{
  private readonly AuctionDbContext _context;
  private readonly IMapper _mapper;
  private readonly IPublishEndpoint _publishEndpoint;

  public AuctionsController(AuctionDbContext context, IMapper mapper, IPublishEndpoint publishEndpoint)
  {
        this._context = context;
        this._mapper = mapper;
        this._publishEndpoint = publishEndpoint;
  }

  [HttpGet]
  public async Task<ActionResult<List<AuctionDto>>> GetAllAuctions(string date)
  {
    var query = this._context.Auctions.OrderBy(x => x.Item.Make).AsQueryable();

    if (!string.IsNullOrEmpty(date))
    {
      query = query.Where(x => x.UpdatedAt.CompareTo(DateTime.Parse(date).ToUniversalTime()) > 0);
    }

    return await query.ProjectTo<AuctionDto>(this._mapper.ConfigurationProvider).ToListAsync();
  }

  [HttpGet("{id}")]
  public async Task<ActionResult<AuctionDto>> GetAuctionById(Guid id)
  {
    var auction = await this._context.Auctions
        .Include(x => x.Item)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (auction is null)
      return this.NotFound();

    return this._mapper.Map<AuctionDto>(auction);
  }

  [Authorize]
  [HttpPost]
  public async Task<ActionResult<AuctionDto>> CreateAuction(CreateAuctionDto auctionDto)
  {
    var auction = this._mapper.Map<Auction>(auctionDto);

    auction.Seller = this.User.Identity.Name;

        this._context.Auctions.Add(auction);

    var newAuction = this._mapper.Map<AuctionDto>(auction);

    await this._publishEndpoint.Publish(this._mapper.Map<AuctionCreated>(newAuction));

    var result = await this._context.SaveChangesAsync() > 0;

    if (!result)
      return this.BadRequest("Could not save changes to the DB");

    return CreatedAtAction(nameof(GetAuctionById),
        new { auction.Id }, newAuction);
  }

  [Authorize]
  [HttpPut("{id}")]
  public async Task<ActionResult> UpdateAuction(Guid id, UpdateAuctionDto updateAuctionDto)
  {
    var auction = await this._context.Auctions
        .Include(x => x.Item)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (auction is null)
      return this.NotFound();

    if (auction.Seller != this.User.Identity.Name)
      return this.Forbid();

    auction.Item.Make = updateAuctionDto.Make ?? auction.Item.Make;
    auction.Item.Model = updateAuctionDto.Model ?? auction.Item.Model;
    auction.Item.Color = updateAuctionDto.Color ?? auction.Item.Color;
    auction.Item.Mileage = updateAuctionDto.Mileage ?? auction.Item.Mileage;
    auction.Item.Year = updateAuctionDto.Year ?? auction.Item.Year;

    await this._publishEndpoint.Publish(this._mapper.Map<AuctionUpdated>(auction));

    var result = await this._context.SaveChangesAsync() > 0;

    if (result)
      return this.Ok();

    return this.BadRequest("Problem saving changes");
  }

  [Authorize]
  [HttpDelete("{id}")]
  public async Task<ActionResult> DeleteAution(Guid id)
  {
    var auction = await this._context.Auctions
        .Include(x => x.Item)
        .FirstOrDefaultAsync(x => x.Id == id);

    if (auction is null)
      return this.NotFound();

    if (auction.Seller != this.User.Identity.Name)
      return this.Forbid();

        this._context.Auctions.Remove(auction);

    await this._publishEndpoint.Publish<AuctionDeleted>(new { Id = auction.Id.ToString() });

    var result = await this._context.SaveChangesAsync() > 0;

    if (!result)
      return this.BadRequest("Could not update DB");

    return this.Ok();
  }
}
