using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.DTOs;

public record UpdateServiceRequestDto(
    string? Title,
    string? Description,
    RequestStatus? Status,
    Priority? Priority,
    Guid? RequesteeId
);
