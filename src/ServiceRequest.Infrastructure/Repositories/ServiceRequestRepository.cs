using Microsoft.EntityFrameworkCore;
using ServiceRequest.Application.Interfaces;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;
using ServiceRequest.Infrastructure.Data;

namespace ServiceRequest.Infrastructure.Repositories;

public class ServiceRequestRepository(AppDbContext db) : IServiceRequestRepository
{
    private IQueryable<ServiceRequestEntity> WithIncludes =>
        db.ServiceRequests.Include(r => r.Requester).Include(r => r.Requestee);

    public async Task<IEnumerable<ServiceRequestEntity>> GetAllAsync(CancellationToken ct = default) =>
        await WithIncludes.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<IEnumerable<ServiceRequestEntity>> GetTopPendingAsync(int count = 3, CancellationToken ct = default) =>
        await WithIncludes
            .Where(r => r.Status == RequestStatus.Open || r.Status == RequestStatus.InProgress)
            .OrderBy(r => r.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<ServiceRequestEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await WithIncludes.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<ServiceRequestEntity> CreateAsync(ServiceRequestEntity entity, CancellationToken ct = default)
    {
        db.ServiceRequests.Add(entity);
        await db.SaveChangesAsync(ct);
        return (await GetByIdAsync(entity.Id, ct))!;
    }

    public async Task<ServiceRequestEntity?> UpdateAsync(Guid id, Action<ServiceRequestEntity> update, CancellationToken ct = default)
    {
        var entity = await db.ServiceRequests.FindAsync([id], ct);
        if (entity is null) return null;
        update(entity);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.ServiceRequests.FindAsync([id], ct);
        if (entity is null) return false;
        db.ServiceRequests.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Id == userId, ct);
}
