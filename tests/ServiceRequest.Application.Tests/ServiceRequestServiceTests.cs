using FluentAssertions;
using NSubstitute;
using ServiceRequest.Application.DTOs;
using ServiceRequest.Application.Interfaces;
using ServiceRequest.Application.Services;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Tests;

public class ServiceRequestServiceTests
{
    private readonly IServiceRequestRepository _repository;
    private readonly ServiceRequestService _sut;

    public ServiceRequestServiceTests()
    {
        _repository = Substitute.For<IServiceRequestRepository>();
        _sut = new ServiceRequestService(_repository);
    }

    private static User MakeUser(string name = "Test User") => new()
    {
        Id    = Guid.NewGuid(),
        Name  = name,
        Email = $"{name.Replace(" ", "").ToLower()}@example.com"
    };

    private static ServiceRequestEntity MakeEntity(User requester, User requestee) => new()
    {
        Id          = Guid.NewGuid(),
        Title       = "Fix the printer",
        Description = "It is broken",
        Status      = RequestStatus.Open,
        Priority    = Priority.High,
        RequesterId = requester.Id,
        Requester   = requester,
        RequesteeId = requestee.Id,
        Requestee   = requestee,
        CreatedAt   = DateTime.UtcNow,
        UpdatedAt   = DateTime.UtcNow
    };

    [Fact]
    public async Task GetAllAsync_ReturnsAllRequests_MappedToDto()
    {
        var requester = MakeUser("Alice");
        var requestee = MakeUser("Bob");
        var entities  = new[] { MakeEntity(requester, requestee), MakeEntity(requester, requestee) };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(entities);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(2);
        result.First().RequesterName.Should().Be("Alice");
        result.First().RequesteeName.Should().Be("Bob");
    }

    [Fact]
    public async Task GetTopPendingAsync_ReturnsTopThreePending()
    {
        var requester = MakeUser("Alice");
        var requestee = MakeUser("Bob");
        var pending = Enumerable.Range(0, 3).Select(_ => MakeEntity(requester, requestee)).ToList();
        _repository.GetTopPendingAsync(3, Arg.Any<CancellationToken>()).Returns(pending);

        var result = await _sut.GetTopPendingAsync();

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r => r.Status.Should().BeOneOf(RequestStatus.Open, RequestStatus.InProgress));
    }

    [Fact]
    public async Task CreateAsync_CreatesAndReturnsDto()
    {
        var requester = MakeUser("Alice");
        var requestee = MakeUser("Bob");
        var dto = new CreateServiceRequestDto(
            "New request",
            "Some description",
            Priority.Medium,
            requester.Id,
            requestee.Id
        );

        var createdEntity = new ServiceRequestEntity
        {
            Id          = Guid.NewGuid(),
            Title       = dto.Title,
            Description = dto.Description,
            Priority    = dto.Priority,
            Status      = RequestStatus.Open,
            RequesterId = dto.RequesterId,
            Requester   = requester,
            RequesteeId = dto.RequesteeId,
            Requestee   = requestee,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };

        _repository
            .CreateAsync(Arg.Any<ServiceRequestEntity>(), Arg.Any<CancellationToken>())
            .Returns(createdEntity);

        var result = await _sut.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Title.Should().Be("New request");
        result.Priority.Should().Be(Priority.Medium);
        result.Status.Should().Be(RequestStatus.Open);
        result.RequesterName.Should().Be("Alice");
        result.RequesteeName.Should().Be("Bob");
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ReturnsNull()
    {
        var missingId = Guid.NewGuid();
        _repository
            .UpdateAsync(missingId, Arg.Any<Action<ServiceRequestEntity>>(), Arg.Any<CancellationToken>())
            .Returns((ServiceRequestEntity?)null);

        var result = await _sut.UpdateAsync(missingId, new UpdateServiceRequestDto("Updated", null, null, null, null));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenFound_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        _repository.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.DeleteAsync(id);

        result.Should().BeTrue();
    }
}
