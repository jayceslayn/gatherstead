using System;

namespace Gatherstead.Data;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
}
