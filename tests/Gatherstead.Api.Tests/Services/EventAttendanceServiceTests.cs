using Gatherstead.Api.Contracts.EventAttendance;
using Gatherstead.Api.Contracts.Responses;
using Gatherstead.Api.Services.Authorization;
using Gatherstead.Api.Services.EventAttendance;
using Gatherstead.Api.Tests.Fixtures;
using Gatherstead.Data;
using Gatherstead.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Gatherstead.Api.Tests.Services;

public class EventAttendanceServiceTests : IAsyncLifetime
{
    private GathersteadDbContext _dbContext = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();

    private static readonly DateOnly Day1 = new(2025, 6, 1);

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.Create(tenantId: _tenantId);
        _dbContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        _dbContext.Properties.Add(new Property { Id = _propertyId, TenantId = _tenantId, Name = "Lake House" });
        _dbContext.Events.Add(new Event
        {
            Id = _eventId,
            TenantId = _tenantId,
            PropertyId = _propertyId,
            Name = "Summer Trip",
            StartDate = Day1,
            EndDate = Day1,
        });
        _dbContext.Households.Add(new Household { Id = _householdId, TenantId = _tenantId, Name = "Smith Household" });
        _dbContext.HouseholdMembers.Add(new HouseholdMember { Id = _member, TenantId = _tenantId, HouseholdId = _householdId, Name = "Alice" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _dbContext.Dispose();
        return ValueTask.CompletedTask;
    }

    private EventAttendanceService CreateService(bool canAssign = true)
    {
        var tenantContext = Mock.Of<ICurrentTenantContext>(c => c.TenantId == _tenantId);
        var auth = Mock.Of<IMemberAuthorizationService>(a =>
            a.CanAssignIntentForMemberAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())
                == Task.FromResult(canAssign));
        return new EventAttendanceService(_dbContext, tenantContext, auth, Mock.Of<IAuditVisibilityContext>());
    }

    private UpsertEventAttendanceRequest Request(Guid memberId) => new()
    {
        HouseholdMemberId = memberId,
        Day = Day1,
        Status = AttendanceStatus.Going,
    };

    [Fact]
    public async Task UpsertAsync_ValidMember_PersistsAttendance()
    {
        var result = await CreateService()
            .UpsertAsync(_tenantId, _eventId, _householdId, Request(_member), TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(AttendanceStatus.Going, result.Entity!.Status);
    }

    [Fact]
    public async Task UpsertAsync_MemberNotFound_ReturnsErrorNotServerError()
    {
        // The reported 500: signing up with a member id that isn't in the household hit the FK
        // constraint on save. The guard must reject it with a validation error instead.
        var result = await CreateService()
            .UpsertAsync(_tenantId, _eventId, _householdId, Request(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("member", StringComparison.OrdinalIgnoreCase));
        Assert.False(await _dbContext.EventAttendances.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpsertAsync_MismatchedHouseholdId_StillSucceeds()
    {
        // Regression for the "Household member not found." bug: the wizard could send a
        // householdId that doesn't match the member's real household. The member exists and the
        // caller is authorized, so the upsert must succeed regardless of the supplied householdId.
        var result = await CreateService()
            .UpsertAsync(_tenantId, _eventId, Guid.NewGuid(), Request(_member), TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Equal(AttendanceStatus.Going, result.Entity!.Status);
    }

    [Fact]
    public async Task UpsertAsync_NotAuthorized_ReturnsPermissionError()
    {
        var result = await CreateService(canAssign: false)
            .UpsertAsync(_tenantId, _eventId, _householdId, Request(_member), TestContext.Current.CancellationToken);

        Assert.False(result.Successful);
        Assert.Contains(result.Messages, m => m.Type == MessageType.ERROR && m.Message.Contains("permission", StringComparison.OrdinalIgnoreCase));
        Assert.False(await _dbContext.EventAttendances.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BulkUpsertAsync_MixedItems_PersistsValidAndReportsPerItemErrors()
    {
        var missingMember = Guid.NewGuid();
        var request = new BulkUpsertEventAttendanceRequest
        {
            Items = new[]
            {
                new UpsertEventAttendanceRequest { HouseholdMemberId = _member, Day = Day1, Status = AttendanceStatus.Going },
                new UpsertEventAttendanceRequest { HouseholdMemberId = missingMember, Day = Day1, Status = AttendanceStatus.Maybe },
            },
        };

        var result = await CreateService().BulkUpsertAsync(_tenantId, _eventId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Single(result.Entity!);
        Assert.Equal(_member, result.Entity!.Single().HouseholdMemberId);
        var itemError = Assert.Single(result.ItemErrors);
        Assert.Equal(1, itemError.Index);
        Assert.Contains("member", itemError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await _dbContext.EventAttendances.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BulkUpsertAsync_ReupsertsSameKey_UpdatesInsteadOfDuplicating()
    {
        var service = CreateService();
        await service.UpsertAsync(_tenantId, _eventId, _householdId, Request(_member), TestContext.Current.CancellationToken);

        var request = new BulkUpsertEventAttendanceRequest
        {
            Items = new[]
            {
                new UpsertEventAttendanceRequest { HouseholdMemberId = _member, Day = Day1, Status = AttendanceStatus.NotGoing },
            },
        };
        var result = await service.BulkUpsertAsync(_tenantId, _eventId, request, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var attendance = Assert.Single(await _dbContext.EventAttendances.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(AttendanceStatus.NotGoing, attendance.Status);
    }

    [Fact]
    public async Task ListAsync_ReturnsAttendancesForEvent()
    {
        // Regression: the list projection must materialize before mapping. The instance MapToDto
        // captures the service, which EF Core rejects in the query shaper — previously a live 500.
        var service = CreateService();
        await service.UpsertAsync(_tenantId, _eventId, _householdId, Request(_member), TestContext.Current.CancellationToken);

        var result = await service.ListAsync(_tenantId, _eventId, null, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        var attendance = Assert.Single(result.Entity!);
        Assert.Equal(_member, attendance.HouseholdMemberId);
        Assert.Equal(AttendanceStatus.Going, attendance.Status);
    }

    [Fact]
    public async Task ListAsync_MemberFilter_ExcludesOtherMembers()
    {
        var service = CreateService();
        await service.UpsertAsync(_tenantId, _eventId, _householdId, Request(_member), TestContext.Current.CancellationToken);

        var result = await service.ListAsync(_tenantId, _eventId, new[] { Guid.NewGuid() }, TestContext.Current.CancellationToken);

        Assert.True(result.Successful);
        Assert.Empty(result.Entity!);
    }
}
