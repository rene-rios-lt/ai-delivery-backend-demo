using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ServiceRequest.Application.DTOs;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceRequest.Api.Tests;

public class ServiceRequestApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("servicerequestdb_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    public Guid RequesterId  { get; private set; }
    public Guid RequesteeId  { get; private set; }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await SeedUsersAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace DbContext registration with test container connection
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString()));

            // Remove the DataSeeder hosted service to prevent seeder from racing with tests
            services.RemoveAll<IHostedService>();

            // Ensure schema is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }

    private async Task SeedUsersAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var requester = new User { Id = Guid.NewGuid(), Name = "Test Requester", Email = "requester@example.com" };
        var requestee = new User { Id = Guid.NewGuid(), Name = "Test Requestee", Email = "requestee@example.com" };

        db.Users.AddRange(requester, requestee);
        await db.SaveChangesAsync();

        RequesterId = requester.Id;
        RequesteeId = requestee.Id;
    }

    public async Task<Guid> SeedRequestAsync(Guid requesterId, Guid requesteeId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = new ServiceRequestEntity
        {
            Id          = Guid.NewGuid(),
            Title       = "Seeded Request",
            Description = "Seeded for testing",
            Status      = RequestStatus.Open,
            Priority    = Priority.Medium,
            RequesterId = requesterId,
            RequesteeId = requesteeId,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        db.ServiceRequests.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }
}

public class ServiceRequestsControllerTests : IClassFixture<ServiceRequestApiFactory>
{
    private readonly HttpClient _client;
    private readonly ServiceRequestApiFactory _factory;

    public ServiceRequestsControllerTests(ServiceRequestApiFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_Returns200()
    {
        // Seed a request so we can assert something is returned
        await _factory.SeedRequestAsync(_factory.RequesterId, _factory.RequesteeId);

        var response = await _client.GetAsync("/api/service-requests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>();
        body.Should().NotBeNull();
        body!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTopPending_Returns200_WithAtMostThreeItems()
    {
        // Seed 5 Open requests so that the endpoint has something to limit
        for (var i = 0; i < 5; i++)
            await _factory.SeedRequestAsync(_factory.RequesterId, _factory.RequesteeId);

        var response = await _client.GetAsync("/api/service-requests/top-pending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>();
        body.Should().NotBeNull();
        body!.Count.Should().BeLessThanOrEqualTo(3);
        body.Count.Should().Be(3);
    }

    [Fact]
    public async Task Post_Returns201_WithCreatedRequest()
    {
        var dto = new CreateServiceRequestDto(
            "Integration test request",
            "Created in integration test",
            Priority.Low,
            _factory.RequesterId,
            _factory.RequesteeId
        );

        var response = await _client.PostAsJsonAsync("/api/service-requests", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<ServiceRequestDto>();
        created.Should().NotBeNull();
        created!.Title.Should().Be("Integration test request");
    }

    [Fact]
    public async Task GetById_WhenFound_Returns200WithBody()
    {
        var seededId = await _factory.SeedRequestAsync(_factory.RequesterId, _factory.RequesteeId);

        var response = await _client.GetAsync($"/api/service-requests/{seededId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ServiceRequestDto>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(seededId);
        body.Title.Should().Be("Seeded Request");
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        var randomId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/service-requests/{randomId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_WhenFound_Returns200WithUpdatedBody()
    {
        var seededId = await _factory.SeedRequestAsync(_factory.RequesterId, _factory.RequesteeId);

        var updateDto = new UpdateServiceRequestDto("Updated Title", null, null, null, null);
        var response  = await _client.PutAsJsonAsync($"/api/service-requests/{seededId}", updateDto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ServiceRequestDto>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task Delete_WhenFound_Returns204()
    {
        var seededId = await _factory.SeedRequestAsync(_factory.RequesterId, _factory.RequesteeId);

        var response = await _client.DeleteAsync($"/api/service-requests/{seededId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        var randomId = Guid.NewGuid();

        var response = await _client.DeleteAsync($"/api/service-requests/{randomId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
