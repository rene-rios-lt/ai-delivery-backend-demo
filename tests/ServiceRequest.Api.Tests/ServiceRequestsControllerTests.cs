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

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
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
}

public class ServiceRequestsControllerTests : IClassFixture<ServiceRequestApiFactory>
{
    private readonly HttpClient _client;

    public ServiceRequestsControllerTests(ServiceRequestApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_Returns200()
    {
        var response = await _client.GetAsync("/api/service-requests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTopPending_Returns200_WithAtMostThreeItems()
    {
        var response = await _client.GetAsync("/api/service-requests/top-pending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>();
        body.Should().NotBeNull();
        body!.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task Post_Returns201_WithCreatedRequest()
    {
        // We need users to exist first — seed two users directly via factory
        // by posting with real user IDs. Since the seeder is disabled in tests,
        // we insert users via a scoped factory method if needed. Instead, we
        // rely on the fact that FK constraints are enforced, so we must insert
        // users first.
        var factory   = new ServiceRequestApiFactory();
        await ((IAsyncLifetime)factory).InitializeAsync();

        try
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var requester = new ServiceRequest.Domain.Entities.User
            {
                Id    = Guid.NewGuid(),
                Name  = "Test Requester",
                Email = "requester@example.com"
            };
            var requestee = new ServiceRequest.Domain.Entities.User
            {
                Id    = Guid.NewGuid(),
                Name  = "Test Requestee",
                Email = "requestee@example.com"
            };
            db.Users.AddRange(requester, requestee);
            await db.SaveChangesAsync();

            var client = factory.CreateClient();
            var dto = new CreateServiceRequestDto(
                "Integration test request",
                "Created in integration test",
                Priority.Low,
                requester.Id,
                requestee.Id
            );

            var response = await client.PostAsJsonAsync("/api/service-requests", dto);

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var created = await response.Content.ReadFromJsonAsync<ServiceRequestDto>();
            created.Should().NotBeNull();
            created!.Title.Should().Be("Integration test request");
            created.Status.Should().Be(RequestStatus.Open);
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }
}
