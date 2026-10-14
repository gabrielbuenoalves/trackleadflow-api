using TrackLeadFlow.Modules.Identity.Contracts.Facades;
using TrackLeadFlow.Modules.WhatsApp.Application.Routing;
using TrackLeadFlow.Modules.WhatsApp.Domain;
using Xunit;

namespace TrackLeadFlow.Modules.WhatsApp.Application.Tests;

public class RouteInboundMessageTests
{
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void Registered_phone_opens_the_operator_conversation()
    {
        var router = new RouteInboundMessage(new FixedPhones("+55 11 98888-7777"));

        var lane = router.Route(TenantId, "5511988887777");

        Assert.Equal(InboundLane.Operator, lane);
    }

    [Fact]
    public void Unknown_phone_enters_triage()
    {
        var router = new RouteInboundMessage(new FixedPhones("+55 11 98888-7777"));

        var lane = router.Route(TenantId, "+55 11 97777-6666");

        Assert.Equal(InboundLane.Lead, lane);
    }

    private sealed class FixedPhones : IOperatorPhones
    {
        private readonly string _phone;

        public FixedPhones(string phone) => _phone = phone;

        public IReadOnlyCollection<string> List(Guid tenantId) => [_phone];
    }
}
