using System.ComponentModel.DataAnnotations;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Application.DTOs;

public record CreateServiceRequestDto(
    [Required] string Title,
    string? Description,
    Priority Priority,
    Guid RequesterId,
    Guid RequesteeId
);
