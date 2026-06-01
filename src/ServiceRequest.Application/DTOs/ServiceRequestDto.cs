namespace ServiceRequest.Application.DTOs;

public record ServiceRequestDto(
    Guid Id,
    string Title,
    string? Description,
    Guid RequesterId,
    string RequesterName,
    Guid RequesteeId,
    string RequesteeName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
