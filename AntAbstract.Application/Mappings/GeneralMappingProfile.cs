using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.DTOs.Conference;
using AntAbstract.Domain.Entities;
using AutoMapper;
using System;

namespace AntAbstract.Application.Mappings
{
    public class GeneralMappingProfile : Profile
    {
        public GeneralMappingProfile()
        {
            CreateMap<Submission, SubmissionDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.CorrespondingAuthorName, opt => opt.MapFrom(src =>
                    src.Author != null ? $"{src.Author.FirstName} {src.Author.LastName}" : "Unknown"))
                .ForMember(dest => dest.ConferenceTitle, opt => opt.MapFrom(src => src.Conference.Title))

                .ForMember(dest => dest.Authors, opt => opt.MapFrom(src => src.SubmissionAuthors))

                .ForMember(dest => dest.Files, opt => opt.MapFrom(src => src.Files));

            CreateMap<SubmissionAuthor, SubmissionAuthorDto>();

            CreateMap<SubmissionFile, SubmissionFileDto>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()));

            CreateMap<SubmissionDto, Submission>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => Enum.Parse<SubmissionStatus>(src.Status)))
                .ForMember(dest => dest.SubmissionAuthors, opt => opt.MapFrom(src => src.Authors))
                .ForMember(dest => dest.Files, opt => opt.MapFrom(src => src.Files));

            CreateMap<SubmissionFileDto, SubmissionFile>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => Enum.Parse<SubmissionFileType>(src.Type)));

            CreateMap<CreateSubmissionDto, Submission>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => SubmissionStatus.New))
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.SubmissionAuthors, opt => opt.Ignore())
                .ForMember(dest => dest.Files, opt => opt.Ignore());
        }
    }
}