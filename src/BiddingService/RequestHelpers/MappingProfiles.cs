namespace BiddingService.RequestHelpers;
using AutoMapper;
using BiddingService.DTOs;
using BiddingService.Models;
using Contracts;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<Bid, BidDto>();
        CreateMap<Bid, BidPlaced>();
    }
}
