using AntAbstract.Application.Interfaces;
using AntAbstract.Application.Mappings;
using AntAbstract.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AntAbstract.Application.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddAutoMapper(cfg => cfg.AddProfile<GeneralMappingProfile>());

            services.AddScoped<ISubmissionService, SubmissionManager>();
            services.AddScoped<IReviewService, ReviewManager>();

            return services;
        }
    }
}
