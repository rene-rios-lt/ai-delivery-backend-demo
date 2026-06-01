using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.DTOs;

public record ServiceRequestDto(
    Guid Id,
    string Title,
    string? Description,
    RequestStatus Status,
    Priority Priority,
    Guid RequesterId,
    string RequesterName,
    Guid RequesteeId,
    string RequesteeName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
