using TrackLeadFlow.Modules.Operator.Domain;
using TrackLeadFlow.Modules.Payments.Contracts.Gateways;

namespace TrackLeadFlow.Modules.Operator.Application;

public sealed record ProposalCommand(
    Guid TenantId,
    Guid LeadId,
    Guid ConversationId,
    int MatchingLeadCount,
    bool LeadIsWithSpecialist,
    string? Label,
    decimal? Amount,
    string? ProposalText,
    string Currency,
    bool InsideServiceWindow,
    bool HasApprovedTemplate,
    bool GatewayAccepted,
    bool ResendPendingCharge);

public interface ILeadMessenger
{
    void SendText(Guid conversationId, string body);
    void SendTemplate(Guid conversationId, string templateName, string body);
}

public sealed class RequestProposal
{
    private readonly IPaymentGateway _gateway;
    private readonly IChargeBook _charges;
    private readonly ILeadMessenger _messenger;

    public RequestProposal(IPaymentGateway gateway, IChargeBook charges, ILeadMessenger messenger)
    {
        _gateway = gateway;
        _charges = charges;
        _messenger = messenger;
    }

    public ProposalDecision Execute(ProposalCommand command)
    {
        var decision = ProposalRequest.Evaluate(
            command.MatchingLeadCount,
            command.LeadIsWithSpecialist,
            command.Label,
            command.Amount,
            command.ProposalText,
            command.InsideServiceWindow,
            command.HasApprovedTemplate,
            command.GatewayAccepted,
            command.ResendPendingCharge);

        var canCharge = decision.Action is ProposalAction.SendTextToLead or ProposalAction.SendTemplateToLead or ProposalAction.RefuseMissingTemplate;
        if (!canCharge || command.Label is null || command.Amount is null || command.ProposalText is null)
        {
            return decision;
        }

        var link = command.ResendPendingCharge
            ? PendingLink(command)
            : _gateway.CreateCharge(command.TenantId, command.Label, command.Amount.Value, command.Currency);

        if (link is null)
        {
            return new ProposalDecision(ProposalAction.RefuseGateway, false);
        }

        if (!command.ResendPendingCharge)
        {
            _charges.Save(command.TenantId, command.LeadId, command.Label, command.Amount.Value, command.Currency, link);
        }

        if (!decision.MessageTheLead)
        {
            return decision;
        }

        var text = command.ProposalText + "\n" + link.CheckoutReference;
        if (decision.Action == ProposalAction.SendTemplateToLead)
        {
            _messenger.SendTemplate(command.ConversationId, "proposal", text);
        }
        else
        {
            _messenger.SendText(command.ConversationId, text);
        }

        return decision;
    }

    private ChargeLink? PendingLink(ProposalCommand command)
    {
        var pending = _charges.FindPending(command.TenantId, command.LeadId);
        return pending?.CheckoutReference is null ? null : new ChargeLink("pending", pending.CheckoutReference);
    }
}
