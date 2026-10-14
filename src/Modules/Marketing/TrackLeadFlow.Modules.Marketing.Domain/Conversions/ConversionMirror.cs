namespace TrackLeadFlow.Modules.Marketing.Domain;

public enum MetaEventName
{
    LeadSubmitted,
    QualifiedLead,
    Purchase,
}

public sealed record MetaEvent(MetaEventName Name, string SourceId, decimal? Value, string? Currency);

public static class ConversionMirror
{
    public static MetaEvent? Contact(string? ctwaClid, string conversationId, bool alreadySent)
    {
        if (alreadySent || string.IsNullOrWhiteSpace(ctwaClid))
        {
            return null;
        }

        return new MetaEvent(MetaEventName.LeadSubmitted, conversationId, null, null);
    }

    public static MetaEvent? Qualified(string? ctwaClid, string leadId, bool alreadySent)
    {
        if (alreadySent || string.IsNullOrWhiteSpace(ctwaClid))
        {
            return null;
        }

        return new MetaEvent(MetaEventName.QualifiedLead, leadId, null, null);
    }

    public static MetaEvent? QueueOpened() => null;

    public static MetaEvent? Disqualified() => null;

    public static MetaEvent? Purchase(string? ctwaClid, string chargeId, decimal value, string currency, bool alreadySent)
    {
        if (alreadySent || string.IsNullOrWhiteSpace(ctwaClid) || string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        return new MetaEvent(MetaEventName.Purchase, chargeId, value, currency);
    }
}
