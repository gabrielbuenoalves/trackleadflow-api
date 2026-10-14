using TrackLeadFlow.Modules.Qualification.Domain;

namespace TrackLeadFlow.Modules.Qualification.Application.Abstractions;

public interface ITriageModel
{
    ModelDecision Decide(string corpus, string transcript);
}

public interface ICorpusEntries
{
    IReadOnlyList<CorpusEntry> List();
}

public enum MetaMirror
{
    QualifiedLead,
}

public interface ITriageEffects
{
    void MarkQualified(Guid leadId);
    void OpenQueue(Guid leadId);
    void ArmFollowUp(Guid leadId);
    void EndFollowUp(Guid leadId);
    void RequestMirror(MetaMirror mirror);
}
