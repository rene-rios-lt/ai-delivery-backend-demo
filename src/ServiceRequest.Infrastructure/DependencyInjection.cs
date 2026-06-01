using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceRequest.Application.Interfaces;
using ServiceRequest.Infrastructure.Data;
using ServiceRequest.Infrastructure.Repositories;

namespace ServiceRequest.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
        services.AddHostedService<DataSeeder>();

        return services;
    }
}
