using TrackLeadFlow.Modules.Operator.Domain;
using Xunit;

namespace TrackLeadFlow.Modules.Operator.Domain.Tests;

public class ProposalRequestTests
{
    [Fact]
    public void Incomplete_request_asks_back_and_does_not_message_the_lead()
    {
        var decision = ProposalRequest.Evaluate(
            matchingLeadCount: 1,
            leadIsWithSpecialist: true,
            label: null,
            amount: 100m,
            proposalText: "Proposta",
            insideServiceWindow: true,
            hasApprovedTemplate: true,
            gatewayAccepted: true,
            resendPendingCharge: false);

        Assert.Equal(ProposalAction.AskBack, decision.Action);
        Assert.False(decision.MessageTheLead);
    }

    [Fact]
    public void Ambiguous_lead_asks_back()
    {
        var decision = ProposalRequest.Evaluate(
            matchingLeadCount: 2,
            leadIsWithSpecialist: true,
            label: "Análise",
            amount: 100m,
            proposalText: "Proposta",
            insideServiceWindow: true,
            hasApprovedTemplate: true,
            gatewayAccepted: true,
            resendPendingCharge: false);

        Assert.Equal(ProposalAction.AskBack, decision.Action);
        Assert.False(decision.MessageTheLead);
    }

    [Fact]
    public void Lead_outside_the_queue_gets_no_message()
    {
        var decision = ProposalRequest.Evaluate(
            matchingLeadCount: 1,
            leadIsWithSpecialist: false,
            label: "Análise",
            amount: 100m,
            proposalText: "Proposta",
            insideServiceWindow: true,
            hasApprovedTemplate: true,
            gatewayAccepted: true,
            resendPendingCharge: false);

        Assert.Equal(ProposalAction.RefuseLeadNotInQueue, decision.Action);
        Assert.False(decision.MessageTheLead);
    }

    [Fact]
    public void Lead_in_the_queue_inside_the_window_gets_the_text()
    {
        var decision = ProposalRequest.Evaluate(
            matchingLeadCount: 1,
            leadIsWithSpecialist: true,
            label: "Análise",
            amount: 100m,
            proposalText: "Proposta",
            insideServiceWindow: true,
            hasApprovedTemplate: false,
            gatewayAccepted: true,
            resendPendingCharge: false);

        Assert.Equal(ProposalAction.SendTextToLead, decision.Action);
        Assert.True(decision.MessageTheLead);
    }

    [Fact]
    public void Outside_the_window_without_a_template_does_not_send()
    {
        var decision = ProposalRequest.Evaluate(
            matchingLeadCount: 1,
            leadIsWithSpecialist: true,
            label: "Análise",
            amount: 100m,
            proposalText: "Proposta",
            insideServiceWindow: false,
            hasApprovedTemplate: false,
            gatewayAccepted: true,
            resendPendingCharge: false);

        Assert.Equal(ProposalAction.RefuseMissingTemplate, decision.Action);
        Assert.False(decision.MessageTheLead);
    }

    [Fact]
    public void Outside_the_window_with_a_template_sends_the_template()
    {
        var decision = ProposalRequest.Evaluate(
            matchingLeadCount: 1,
            leadIsWithSpecialist: true,
            label: "Análise",
            amount: 100m,
            proposalText: "Proposta",
            insideServiceWindow: false,
            hasApprovedTemplate: true,
            gatewayAccepted: true,
            resendPendingCharge: false);

        Assert.Equal(ProposalAction.SendTemplateToLead, decision.Action);
        Assert.True(decision.MessageTheLead);
    }

    [Fact]
    public void Gateway_refusal_does_not_send_the_link()
    {
        var decision = ProposalRequest.Evaluate(
            matchingLeadCount: 1,
            leadIsWithSpecialist: true,
            label: "Análise",
            amount: 100m,
            proposalText: "Proposta",
            insideServiceWindow: true,
            hasApprovedTemplate: true,
            gatewayAccepted: false,
            resendPendingCharge: false);

        Assert.Equal(ProposalAction.RefuseGateway, decision.Action);
        Assert.False(decision.MessageTheLead);
    }
}
