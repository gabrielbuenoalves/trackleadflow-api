using TrackLeadFlow.Modules.Qualification.Application.Abstractions;
using TrackLeadFlow.Modules.Qualification.Domain;

namespace TrackLeadFlow.Modules.Qualification.Application.Triage;

public sealed record TriageContext(
    Guid TenantId,
    Guid LeadId,
    Guid ConversationId,
    LeadTriagePhase Phase,
    string? CtwaClid,
    bool QualifiedAlreadySent);

public sealed record TriageAdvanceResult(LeadTriagePhase Phase, string? ReplyText);

public sealed class AdvanceTriage
{
    private readonly ICorpusEntries _entries;
    private readonly ITriageModel _model;
    private readonly ITriageEffects _effects;

    public AdvanceTriage(ICorpusEntries entries, ITriageModel model, ITriageEffects effects)
    {
        _entries = entries;
        _model = model;
        _effects = effects;
    }

    public TriageAdvanceResult Execute(TriageContext context, string transcript)
    {
        ModelDecision decision;
        try
        {
            var corpus = TenantCorpus.Compose(context.TenantId, _entries.List());
            decision = _model.Decide(corpus, transcript);
        }
        catch (InvalidOperationException)
        {
            return new TriageAdvanceResult(context.Phase, null);
        }

        var turn = TriageTurn.Apply(context.Phase, decision);
        foreach (var signal in turn.Signals)
        {
            switch (signal)
            {
                case TurnSignal.MarkQualified:
                    _effects.MarkQualified(context.LeadId);
                    if (!context.QualifiedAlreadySent && !string.IsNullOrWhiteSpace(context.CtwaClid))
                    {
                        _effects.RequestMirror(MetaMirror.QualifiedLead);
                    }
                    break;
                case TurnSignal.OpenSpecialistQueue:
                    _effects.OpenQueue(context.LeadId);
                    break;
                case TurnSignal.ArmFollowUp when turn.Phase == LeadTriagePhase.Qualified:
                    _effects.ArmFollowUp(context.LeadId);
                    break;
                case TurnSignal.EndFollowUp:
                    _effects.EndFollowUp(context.LeadId);
                    break;
            }
        }

        return new TriageAdvanceResult(turn.Phase, turn.ReplyText);
    }

    public void OnSilence(TriageContext context)
    {
        if (TriageTurn.ArmsFollowUpOnSilence(context.Phase))
        {
            _effects.ArmFollowUp(context.LeadId);
        }
    }
}
