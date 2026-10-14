namespace TrackLeadFlow.Modules.Payments.Contracts.Gateways;

public sealed record ChargeLink(string ExternalChargeId, string CheckoutReference);

public interface IPaymentGateway
{
    ChargeLink? CreateCharge(Guid tenantId, string label, decimal amount, string currency);
}

public sealed record StoredCharge(Guid Id, string Label, decimal Amount, string? CheckoutReference, string Status);

public interface IChargeBook
{
    StoredCharge? FindPending(Guid tenantId, Guid leadId);
    void Save(Guid tenantId, Guid leadId, string label, decimal amount, string currency, ChargeLink link);
}
