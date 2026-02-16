using System;

namespace Gatherstead.Data;

public interface ICurrentTenantContext
{
    Guid? TenantId { get; }
}
