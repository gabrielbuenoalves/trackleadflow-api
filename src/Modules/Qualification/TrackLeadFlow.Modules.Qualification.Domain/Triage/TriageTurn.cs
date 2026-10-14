namespace TrackLeadFlow.Modules.Qualification.Domain;

public enum TriageSituation
{
    InConversation,
    Qualified,
    Disqualified,
}

public enum WillingnessToPay
{
    Unknown,
    Yes,
    No,
}

public enum LeadTriagePhase
{
    InConversation,
    Qualified,
    WithSpecialist,
    Disqualified,
}

public enum TurnSignal
{
    MarkQualified,
    OpenSpecialistQueue,
    StopAi,
    EndFollowUp,
    ArmFollowUp,
    EndTriage,
}

public sealed record ModelDecision(
    TriageSituation Situation,
    WillingnessToPay WillingnessToPay,
    string? ReplyText,
    string? DisplayName,
    string? DocumentNumber);

public sealed record TriageTurnResult(
    bool SendGeneratedReply,
    string? ReplyText,
    LeadTriagePhase Phase,
    IReadOnlyList<TurnSignal> Signals,
    string? DisplayName,
    string? DocumentNumber);

public static class TriageTurn
{
    public static TriageTurnResult Apply(LeadTriagePhase phase, ModelDecision? decision)
    {
        if (decision is null || phase is LeadTriagePhase.WithSpecialist or LeadTriagePhase.Disqualified)
        {
            return Unchanged(phase);
        }

        var send = !string.IsNullOrWhiteSpace(decision.ReplyText);
        var reply = send ? decision.ReplyText : null;

        return decision.Situation switch
        {
            TriageSituation.InConversation => new TriageTurnResult(
                send,
                reply,
                phase == LeadTriagePhase.Qualified ? LeadTriagePhase.Qualified : LeadTriagePhase.InConversation,
                [],
                decision.DisplayName,
                decision.DocumentNumber),
            TriageSituation.Qualified when decision.WillingnessToPay == WillingnessToPay.Yes => Handoff(phase, send, reply, decision),
            TriageSituation.Qualified => StayQualified(phase, send, reply, decision),
            TriageSituation.Disqualified => new TriageTurnResult(
                send,
                reply,
                LeadTriagePhase.Disqualified,
                [TurnSignal.EndTriage, TurnSignal.EndFollowUp],
                decision.DisplayName,
                decision.DocumentNumber),
            _ => Unchanged(phase),
        };
    }

    public static bool ArmsFollowUpOnSilence(LeadTriagePhase phase) =>
        phase == LeadTriagePhase.Qualified;

    private static TriageTurnResult Handoff(LeadTriagePhase phase, bool send, string? reply, ModelDecision decision)
    {
        var signals = new List<TurnSignal>();
        if (phase != LeadTriagePhase.Qualified)
        {
            signals.Add(TurnSignal.MarkQualified);
        }

        signals.Add(TurnSignal.OpenSpecialistQueue);
        signals.Add(TurnSignal.StopAi);
        signals.Add(TurnSignal.EndFollowUp);
        return new TriageTurnResult(
            send,
            reply,
            LeadTriagePhase.WithSpecialist,
            signals,
            decision.DisplayName,
            decision.DocumentNumber);
    }

    private static TriageTurnResult StayQualified(LeadTriagePhase phase, bool send, string? reply, ModelDecision decision)
    {
        var signals = new List<TurnSignal>();
        if (phase != LeadTriagePhase.Qualified)
        {
            signals.Add(TurnSignal.MarkQualified);
        }

        signals.Add(TurnSignal.ArmFollowUp);
        return new TriageTurnResult(
            send,
            reply,
            LeadTriagePhase.Qualified,
            signals,
            decision.DisplayName,
            decision.DocumentNumber);
    }

    private static TriageTurnResult Unchanged(LeadTriagePhase phase) =>
        new(false, null, phase, [], null, null);
}
