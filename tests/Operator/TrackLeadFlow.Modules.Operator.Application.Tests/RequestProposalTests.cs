using TrackLeadFlow.Modules.Operator.Application;
using TrackLeadFlow.Modules.Operator.Domain;
using TrackLeadFlow.Modules.Payments.Contracts.Gateways;
using Xunit;

namespace TrackLeadFlow.Modules.Operator.Application.Tests;

public sealed class RequestProposalUseTests
{
    private readonly FakeGateway _gateway = new();
    private readonly MemoryBook _charges = new();
    private readonly FakeMessenger _messenger = new();

    [Fact]
    public void An_incomplete_or_out_of_queue_request_does_not_charge_or_message()
    {
        Execute(matchingLeadCount: 2, leadIsWithSpecialist: true, label: "Análise", amount: 10m, text: "proposta");
        Execute(matchingLeadCount: 1, leadIsWithSpecialist: false, label: "Análise", amount: 10m, text: "proposta");
        Execute(matchingLeadCount: 1, leadIsWithSpecialist: true, label: null, amount: null, text: null);

        Assert.Equal(0, _gateway.Calls);
        Assert.Empty(_charges.Saved);
        Assert.Empty(_messenger.Texts);
        Assert.Empty(_messenger.Templates);
    }

    [Fact]
    public void A_valid_request_inside_the_window_stores_the_link_and_sends_text()
    {
        var decision = Execute(matchingLeadCount: 1, leadIsWithSpecialist: true, label: "Análise", amount: 150m, text: "Segue a proposta");

        Assert.Equal(ProposalAction.SendTextToLead, decision.Action);
        Assert.Equal(1, _gateway.Calls);
        Assert.Equal("https://pay/1", _charges.Saved[0].CheckoutReference);
        Assert.Single(_messenger.Texts);
        Assert.Empty(_messenger.Templates);
    }

    [Fact]
    public void Outside_the_window_without_a_template_keeps_the_charge_and_does_not_message()
    {
        var decision = Execute(matchingLeadCount: 1, leadIsWithSpecialist: true, label: "Análise", amount: 150m, text: "Segue a proposta", insideWindow: false, hasTemplate: false);

        Assert.Equal(ProposalAction.RefuseMissingTemplate, decision.Action);
        Assert.Single(_charges.Saved);
        Assert.Empty(_messenger.Texts);
        Assert.Empty(_messenger.Templates);
    }

    [Fact]
    public void A_resend_reuses_the_pending_charge_and_a_new_label_creates_another()
    {
        Execute(matchingLeadCount: 1, leadIsWithSpecialist: true, label: "Análise", amount: 150m, text: "Segue a proposta");
        Execute(matchingLeadCount: 1, leadIsWithSpecialist: true, label: "Análise", amount: 150m, text: "Segue a proposta", resend: true);
        Execute(matchingLeadCount: 1, leadIsWithSpecialist: true, label: "Contrato", amount: 900m, text: "Nova proposta");

        Assert.Equal(2, _gateway.Calls);
        Assert.Equal(2, _charges.Saved.Count);
        Assert.Equal("https://pay/1", _charges.Saved[0].CheckoutReference);
        Assert.Equal("Contrato", _charges.Saved[1].Label);
        Assert.Contains("https://pay/1", _messenger.Texts[1], StringComparison.Ordinal);
    }

    private ProposalDecision Execute(
        int matchingLeadCount,
        bool leadIsWithSpecialist,
        string? label,
        decimal? amount,
        string? text,
        bool insideWindow = true,
        bool hasTemplate = true,
        bool gatewayAccepted = true,
        bool resend = false)
    {
        var useCase = new RequestProposal(_gateway, _charges, _messenger);
        return useCase.Execute(new ProposalCommand(
            Guid.Empty,
            Guid.Empty,
            Guid.CreateVersion7(),
            matchingLeadCount,
            leadIsWithSpecialist,
            label,
            amount,
            text,
            "BRL",
            insideWindow,
            hasTemplate,
            gatewayAccepted,
            resend));
    }

    private sealed class FakeGateway : IPaymentGateway
    {
        public int Calls { get; private set; }
        public ChargeLink? CreateCharge(Guid tenantId, string label, decimal amount, string currency)
        {
            Calls++;
            return new ChargeLink("ext-" + Calls, "https://pay/" + Calls);
        }
    }

    private sealed class MemoryBook : IChargeBook
    {
        public List<StoredCharge> Saved { get; } = [];
        public StoredCharge? FindPending(Guid tenantId, Guid leadId) => Saved.LastOrDefault(charge => charge.Status == "Pending");
        public void Save(Guid tenantId, Guid leadId, string label, decimal amount, string currency, ChargeLink link)
        {
            Saved.Add(new StoredCharge(Guid.CreateVersion7(), label, amount, link.CheckoutReference, "Pending"));
        }
    }

    private sealed class FakeMessenger : ILeadMessenger
    {
        public List<string> Texts { get; } = [];
        public List<string> Templates { get; } = [];
        public void SendText(Guid conversationId, string body) => Texts.Add(body);
        public void SendTemplate(Guid conversationId, string templateName, string body) => Templates.Add(templateName);
    }
}
