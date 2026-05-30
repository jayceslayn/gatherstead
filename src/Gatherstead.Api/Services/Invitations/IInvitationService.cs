using Gatherstead.Api.Contracts.Invitations;
using Gatherstead.Api.Contracts.Responses;

namespace Gatherstead.Api.Services.Invitations;

public interface IInvitationService
{
    Task<InvitationResponse> CreateAsync(
        Guid tenantId,
        CreateInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<BaseEntityResponse<IReadOnlyCollection<InvitationDto>>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<InvitationResponse> RevokeAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancellationToken = default);
}
