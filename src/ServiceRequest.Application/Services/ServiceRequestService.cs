using ServiceRequest.Application.DTOs;
using ServiceRequest.Application.Interfaces;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.Services;

public class ServiceRequestService(IServiceRequestRepository repository)
{
    public async Task<IEnumerable<ServiceRequestDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await repository.GetAllAsync(ct);
        return entities.Select(ToDto);
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetTopPendingAsync(CancellationToken ct = default)
    {
        var entities = await repository.GetTopPendingAsync(3, ct);
        return entities.Select(ToDto);
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<ServiceRequestDto> CreateAsync(CreateServiceRequestDto dto, CancellationToken ct = default)
    {
        var entity = new ServiceRequestEntity
        {
            Title       = dto.Title,
            Description = dto.Description,
            Priority    = dto.Priority,
            RequesterId = dto.RequesterId,
            RequesteeId = dto.RequesteeId,
        };
        var created = await repository.CreateAsync(entity, ct);
        return ToDto(created);
    }

    public async Task<ServiceRequestDto?> UpdateAsync(Guid id, UpdateServiceRequestDto dto, CancellationToken ct = default)
    {
        var updated = await repository.UpdateAsync(id, e =>
        {
            if (dto.Title is not null)       e.Title       = dto.Title;
            if (dto.Description is not null) e.Description = dto.Description;
            if (dto.Status is not null)      e.Status      = dto.Status.Value;
            if (dto.Priority is not null)    e.Priority    = dto.Priority.Value;
            if (dto.RequesteeId is not null) e.RequesteeId = dto.RequesteeId.Value;
            e.UpdatedAt = DateTime.UtcNow;
        }, ct);
        return updated is null ? null : ToDto(updated);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        repository.DeleteAsync(id, ct);

    private static ServiceRequestDto ToDto(ServiceRequestEntity e) => new(
        e.Id,
        e.Title,
        e.Description,
        e.Status,
        e.Priority,
        e.RequesterId,
        e.Requester?.Name ?? string.Empty,
        e.RequesteeId,
        e.Requestee?.Name ?? string.Empty,
        e.CreatedAt,
        e.UpdatedAt
    );
}
