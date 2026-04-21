using Gatherstead.Data.Entities;

namespace Gatherstead.Api.Contracts.Tenants;

public record TenantSummary(Guid Id, string Name, TenantRole? UserRole = null);
