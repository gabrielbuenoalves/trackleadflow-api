namespace TrackLeadFlow.Modules.Qualification.Domain;

public sealed record CorpusEntry(Guid TenantId, string Text);

public static class TenantCorpus
{
    public static string Compose(Guid tenantId, IEnumerable<CorpusEntry> entries) =>
        string.Join('\n', entries.Where(entry => entry.TenantId == tenantId).Select(entry => entry.Text));
}
