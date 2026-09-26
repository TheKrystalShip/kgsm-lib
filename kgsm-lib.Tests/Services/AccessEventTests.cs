using System.Text.Json;

using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// The auth anchor's access events: classified in the catalog, and read back from the payloads the
/// anchor's journal writes.
/// </summary>
public class AccessEventTests
{
    [Theory]
    [InlineData("auth.assignment.granted", typeof(AssignmentEventData), EventSubject.Account)]
    [InlineData("auth.assignment.revoked", typeof(AssignmentEventData), EventSubject.Account)]
    [InlineData("auth.role.changed", typeof(AuthorityRecordEventData), EventSubject.Access)]
    [InlineData("auth.role.removed", typeof(AuthorityRecordEventData), EventSubject.Access)]
    [InlineData("auth.permission.changed", typeof(AuthorityRecordEventData), EventSubject.Access)]
    [InlineData("auth.permission.removed", typeof(AuthorityRecordEventData), EventSubject.Access)]
    [InlineData("auth.catalog.changed", typeof(CatalogEventData), EventSubject.Access)]
    [InlineData("auth.service.requirement.approved", typeof(ServiceRequirementEventData), EventSubject.Access)]
    [InlineData("auth.service.requirement.revoked", typeof(ServiceRequirementEventData), EventSubject.Access)]
    public void EveryAccessEvent_IsClassified(string type, Type payload, EventSubject subject)
    {
        EventDescriptor descriptor = KgsmEventCatalog.Describe(type);

        Assert.True(descriptor.Known);
        Assert.Equal(payload, descriptor.PayloadType);
        Assert.Equal(subject, descriptor.Subject);
        Assert.Contains(descriptor.Fields, f => f.Name == "AuthorityVersion");
    }

    [Fact]
    public void AnAssignment_ReadsBackFromTheAnchorsPayload()
    {
        const string json = """
            {"AssignmentId":"asg_1","UserId":"usr_1","Username":null,"RoleId":"role_1","Role":"Hosts",
             "Scope":"instance:walter/terraria#9f3c","AuthorityVersion":12,"Actor":"local:owner","Origin":"ui"}
            """;

        var data = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.AssignmentEventData)!;

        Assert.Equal("asg_1", data.AssignmentId);
        Assert.Equal("usr_1", data.UserId);
        Assert.Equal("Hosts", data.Role);
        Assert.Equal("instance:walter/terraria#9f3c", data.Scope);
        Assert.Equal(12, data.AuthorityVersion);
        Assert.Equal("local:owner", data.Actor);
    }

    [Fact]
    public void ACatalogChange_ReadsBackItsArrivalsAndDepartures()
    {
        const string json = """
            {"Member":"walter","Added":["reactor:rules.write"],"Removed":[],"AuthorityVersion":3,"Actor":"system:walter"}
            """;

        var data = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.CatalogEventData)!;

        Assert.Equal("walter", data.Member);
        Assert.Equal(["reactor:rules.write"], data.Added!);
        Assert.Empty(data.Removed!);
    }

    [Fact]
    public void ARequirement_SaysWhetherAnybodyDecidedIt()
    {
        const string json = """
            {"AccountId":"usr_svc","Service":null,"Action":"kgsm:server.restart","Scope":"node:walter","Automatic":true,"AuthorityVersion":4}
            """;

        var data = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.ServiceRequirementEventData)!;

        Assert.True(data.Automatic);
        Assert.Equal("node:walter", data.Scope);
        Assert.Null(data.Service);
    }
}
