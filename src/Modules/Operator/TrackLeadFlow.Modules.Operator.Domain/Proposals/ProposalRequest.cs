namespace TrackLeadFlow.Modules.Operator.Domain;

public enum ProposalAction
{
    AskBack,
    SendTextToLead,
    SendTemplateToLead,
    RefuseLeadNotInQueue,
    RefuseMissingTemplate,
    RefuseGateway,
}

public sealed record ProposalDecision(ProposalAction Action, bool MessageTheLead);

public static class ProposalRequest
{
    public static ProposalDecision Evaluate(
        int matchingLeadCount,
        bool leadIsWithSpecialist,
        string? label,
        decimal? amount,
        string? proposalText,
        bool insideServiceWindow,
        bool hasApprovedTemplate,
        bool gatewayAccepted,
        bool resendPendingCharge)
    {
        var missingFields = string.IsNullOrWhiteSpace(label)
            || amount is null
            || string.IsNullOrWhiteSpace(proposalText);
        if (matchingLeadCount != 1 || (!resendPendingCharge && missingFields))
        {
            return new ProposalDecision(ProposalAction.AskBack, false);
        }

        if (!leadIsWithSpecialist)
        {
            return new ProposalDecision(ProposalAction.RefuseLeadNotInQueue, false);
        }

        if (!resendPendingCharge && !gatewayAccepted)
        {
            return new ProposalDecision(ProposalAction.RefuseGateway, false);
        }

        if (!insideServiceWindow && !hasApprovedTemplate)
        {
            return new ProposalDecision(ProposalAction.RefuseMissingTemplate, false);
        }

        if (!insideServiceWindow)
        {
            return new ProposalDecision(ProposalAction.SendTemplateToLead, true);
        }

        return new ProposalDecision(ProposalAction.SendTextToLead, true);
    }
}
