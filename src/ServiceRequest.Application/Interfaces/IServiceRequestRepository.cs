using ServiceRequest.Domain.Entities;

namespace ServiceRequest.Application.Interfaces;

public interface IServiceRequestRepository
{
    Task<IEnumerable<ServiceRequestEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<ServiceRequestEntity>> GetTopPendingAsync(int count = 3, CancellationToken ct = default);
    Task<ServiceRequestEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ServiceRequestEntity> CreateAsync(ServiceRequestEntity entity, CancellationToken ct = default);
    Task<ServiceRequestEntity?> UpdateAsync(Guid id, Action<ServiceRequestEntity> update, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);
}
