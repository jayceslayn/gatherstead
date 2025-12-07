using System;

namespace Gatherstead.Db;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
}
