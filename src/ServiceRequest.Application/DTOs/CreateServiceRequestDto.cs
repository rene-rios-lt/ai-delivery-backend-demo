using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.DTOs;

public record CreateServiceRequestDto(
    string Title,
    string? Description,
    Priority Priority,
    Guid RequesterId,
    Guid RequesteeId
);
