using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Infrastructure.Data;

public class DataSeeder(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(ct);

        if (await db.Users.AnyAsync(ct)) return;

        var faker = new Faker();
        faker.Random = new Bogus.Randomizer(42);

        var users = Enumerable.Range(0, 5).Select(_ => new User
        {
            Id    = Guid.NewGuid(),
            Name  = faker.Name.FullName(),
            Email = faker.Internet.Email()
        }).ToList();

        await db.Users.AddRangeAsync(users, ct);
        await db.SaveChangesAsync(ct);

        var statuses = new[]
        {
            RequestStatus.Open, RequestStatus.Open, RequestStatus.Open,
            RequestStatus.InProgress, RequestStatus.InProgress,
            RequestStatus.InProgress, RequestStatus.InProgress,
            RequestStatus.Completed, RequestStatus.Completed, RequestStatus.Completed
        };

        var requests = statuses.Select((status, i) => new ServiceRequestEntity
        {
            Id          = Guid.NewGuid(),
            Title       = faker.Hacker.Phrase(),
            Description = faker.Lorem.Paragraph(),
            Status      = status,
            Priority    = faker.PickRandom<Priority>(),
            RequesterId = faker.PickRandom(users).Id,
            RequesteeId = faker.PickRandom(users).Id,
            CreatedAt   = DateTime.UtcNow.AddDays(-(10 - i)),
            UpdatedAt   = DateTime.UtcNow
        }).ToList();

        await db.ServiceRequests.AddRangeAsync(requests, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
