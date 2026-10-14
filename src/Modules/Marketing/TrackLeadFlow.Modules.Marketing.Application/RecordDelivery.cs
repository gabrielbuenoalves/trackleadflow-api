using TrackLeadFlow.Modules.Marketing.Domain;

namespace TrackLeadFlow.Modules.Marketing.Application;

public interface IDeliverySink
{
    bool AlreadyRecorded(Guid tenantId, string businessEvent, Guid sourceId);
    void InsertDelivery(Guid tenantId, string businessEvent, Guid sourceId, string metaEventName, string status, string? skipReason);
    void InsertAttribution(Guid tenantId, Guid conversationId, string ctwaClid);
}

public sealed class RecordDelivery
{
    public void ContactStarted(IDeliverySink sink, Guid tenantId, Guid conversationId, string? ctwaClid)
    {
        Write(sink, tenantId, conversationId, "LeadSubmitted", ConversionMirror.Contact(ctwaClid, conversationId.ToString(), alreadySent: false), ctwaClid, conversationId, attribute: true);
    }

    public void Qualified(IDeliverySink sink, Guid tenantId, Guid leadId, string? ctwaClid)
    {
        Write(sink, tenantId, leadId, "QualifiedLead", ConversionMirror.Qualified(ctwaClid, leadId.ToString(), alreadySent: false), ctwaClid, conversationId: Guid.Empty, attribute: false);
    }

    public void Purchase(IDeliverySink sink, Guid tenantId, Guid chargeId, string? ctwaClid, decimal value, string currency)
    {
        Write(sink, tenantId, chargeId, "Purchase", ConversionMirror.Purchase(ctwaClid, chargeId.ToString(), value, currency, alreadySent: false), ctwaClid, conversationId: Guid.Empty, attribute: false);
    }

    private static void Write(IDeliverySink sink, Guid tenantId, Guid sourceId, string businessEvent, MetaEvent? mirror, string? ctwaClid, Guid conversationId, bool attribute)
    {
        if (sink.AlreadyRecorded(tenantId, businessEvent, sourceId))
        {
            return;
        }

        if (mirror is null)
        {
            sink.InsertDelivery(tenantId, businessEvent, sourceId, businessEvent, "Skipped", "missing ctwa_clid");
            return;
        }

        if (attribute && !string.IsNullOrWhiteSpace(ctwaClid))
        {
            sink.InsertAttribution(tenantId, conversationId, ctwaClid);
        }

        sink.InsertDelivery(tenantId, businessEvent, sourceId, mirror.Name.ToString(), "Pending", null);
    }
}
