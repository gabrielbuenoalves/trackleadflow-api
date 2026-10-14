using TrackLeadFlow.Modules.Marketing.Application;

namespace TrackLeadFlow.Modules.Marketing.Infrastructure.Persistence;

public sealed class PostgresDeliverySink : IDeliverySink
{
    private readonly MarketingRepository _repository;
    private readonly DateTimeOffset _now;

    public PostgresDeliverySink(MarketingRepository repository, DateTimeOffset now)
    {
        _repository = repository;
        _now = now;
    }

    public bool AlreadyRecorded(Guid tenantId, string businessEvent, Guid sourceId) =>
        _repository.DeliveryExists(tenantId, businessEvent, sourceId);

    public void InsertDelivery(Guid tenantId, string businessEvent, Guid sourceId, string metaEventName, string status, string? skipReason)
    {
        _repository.InsertDelivery(Guid.CreateVersion7(), tenantId, businessEvent, sourceId, metaEventName, status, skipReason, _now);
    }

    public void InsertAttribution(Guid tenantId, Guid conversationId, string ctwaClid)
    {
        _repository.InsertAttribution(Guid.CreateVersion7(), tenantId, conversationId, ctwaClid, _now);
    }
}
