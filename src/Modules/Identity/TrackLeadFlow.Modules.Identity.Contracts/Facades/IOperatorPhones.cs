namespace TrackLeadFlow.Modules.Identity.Contracts.Facades;

public interface IOperatorPhones
{
    IReadOnlyCollection<string> List(Guid tenantId);
}
