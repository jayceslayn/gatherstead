using System;

namespace Gatherstead.Db;

public interface ICurrentTenantContext
{
    Guid? TenantId { get; }
}
