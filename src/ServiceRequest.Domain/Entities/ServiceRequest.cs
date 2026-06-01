using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Domain.Entities;

public class ServiceRequestEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public Priority Priority { get; set; } = Priority.Medium;
    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = null!;
    public Guid RequesteeId { get; set; }
    public User Requestee { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
