namespace TrackLeadFlow.Modules.Payments.Infrastructure.Persistence;

public sealed record ChargeRow(Guid Id, Guid TenantId, decimal Amount, string Currency, string Status);
