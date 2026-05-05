using AutoMapper;
using EWallet.Application.DTOs;
using EWallet.Domain.Entities;

namespace EWallet.Application.Mappers;

public class WalletMappingProfile : Profile
{
    public WalletMappingProfile()
    {
        // Wallet → WalletDto (flatten Money value object)
        CreateMap<Wallet, WalletDto>()
            .ForMember(dest => dest.Balance,  opt => opt.MapFrom(src => src.Balance.Amount))
            .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Balance.Currency));

        // Transaction → TransactionDto (flatten Money value object)
        CreateMap<Transaction, TransactionDto>()
            .ForMember(dest => dest.Amount,   opt => opt.MapFrom(src => src.Amount.Amount))
            .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Amount.Currency));

        // User → UserProfileDto
        CreateMap<User, UserProfileDto>();
    }
}
