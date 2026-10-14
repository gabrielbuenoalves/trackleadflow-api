using TrackLeadFlow.Modules.Identity.Contracts.Facades;
using TrackLeadFlow.Modules.WhatsApp.Domain;

namespace TrackLeadFlow.Modules.WhatsApp.Application.Routing;

public sealed class RouteInboundMessage
{
    private readonly IOperatorPhones _operatorPhones;

    public RouteInboundMessage(IOperatorPhones operatorPhones)
    {
        _operatorPhones = operatorPhones;
    }

    public InboundLane Route(Guid tenantId, string senderPhone) =>
        InboundRoute.Resolve(senderPhone, _operatorPhones.List(tenantId));
}
