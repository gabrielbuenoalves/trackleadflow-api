using TrackLeadFlow.Modules.Qualification.Application.Abstractions;
using TrackLeadFlow.Modules.Qualification.Application.Triage;
using TrackLeadFlow.Modules.Qualification.Domain;
using Xunit;

namespace TrackLeadFlow.Modules.Qualification.Application.Tests;

public sealed class AdvanceTriageTests
{
    private readonly Guid _tenantId = Guid.CreateVersion7();
    private readonly Guid _leadId = Guid.CreateVersion7();
    private readonly Guid _conversationId = Guid.CreateVersion7();

    [Fact]
    public void A_model_failure_does_not_change_the_phase()
    {
        var effects = new RecordingEffects();
        var triage = new AdvanceTriage(Entries(("script", _tenantId)), new BrokenModel(), effects);
        var result = triage.Execute(Context(LeadTriagePhase.InConversation, "click-1", false), "oi");

        Assert.Equal(LeadTriagePhase.InConversation, result.Phase);
        Assert.Empty(effects.Qualified);
        Assert.Empty(effects.Queues);
        Assert.Empty(effects.Armed);
    }

    [Fact]
    public void Another_tenants_corpus_does_not_enter_the_prompt()
    {
        var model = new RecordingModel(new ModelDecision(TriageSituation.InConversation, WillingnessToPay.Unknown, "olá", null, null));
        var triage = new AdvanceTriage(
            Entries(("script do cliente", _tenantId), ("segredo de outro", Guid.CreateVersion7())),
            model,
            new RecordingEffects());

        triage.Execute(Context(LeadTriagePhase.InConversation, "click-1", false), "oi");

        Assert.Contains("script do cliente", model.Corpus, StringComparison.Ordinal);
        Assert.DoesNotContain("segredo de outro", model.Corpus, StringComparison.Ordinal);
    }

    [Fact]
    public void Qualified_and_willing_opens_the_queue_without_a_second_event()
    {
        var effects = new RecordingEffects();
        var model = new RecordingModel(new ModelDecision(TriageSituation.Qualified, WillingnessToPay.Yes, "vou te passar", "Ana", null));
        var triage = new AdvanceTriage(Entries(("script", _tenantId)), model, effects);

        var result = triage.Execute(Context(LeadTriagePhase.InConversation, "click-1", false), "quero pagar");

        Assert.Equal(LeadTriagePhase.WithSpecialist, result.Phase);
        Assert.Equal([_leadId], effects.Qualified);
        Assert.Equal([_leadId], effects.Queues);
        Assert.Equal([MetaMirror.QualifiedLead], effects.Events);
    }

    [Fact]
    public void Disqualification_ends_triage_without_an_event()
    {
        var effects = new RecordingEffects();
        var model = new RecordingModel(new ModelDecision(TriageSituation.Disqualified, WillingnessToPay.No, "não atendo", null, null));
        var triage = new AdvanceTriage(Entries(("script", _tenantId)), model, effects);

        var result = triage.Execute(Context(LeadTriagePhase.InConversation, "click-1", false), "não");

        Assert.Equal(LeadTriagePhase.Disqualified, result.Phase);
        Assert.Empty(effects.Events);
        Assert.Empty(effects.Queues);
        Assert.Equal([_leadId], effects.EndedFollowUp);
    }

    [Fact]
    public void Silence_before_qualification_does_not_arm_follow_up()
    {
        var effects = new RecordingEffects();
        var triage = new AdvanceTriage(Entries(("script", _tenantId)), new RecordingModel(null), effects);

        triage.OnSilence(Context(LeadTriagePhase.InConversation, "click-1", false));
        triage.OnSilence(Context(LeadTriagePhase.Qualified, "click-1", true));

        Assert.Equal([_leadId], effects.Armed);
    }

    private TriageContext Context(LeadTriagePhase phase, string? ctwaClid, bool qualifiedAlreadySent) =>
        new(_tenantId, _leadId, _conversationId, phase, ctwaClid, qualifiedAlreadySent);

    private static ICorpusEntries Entries(params (string Text, Guid TenantId)[] entries) =>
        new FixedCorpus(entries.Select(entry => new CorpusEntry(entry.TenantId, entry.Text)).ToArray());

    private sealed class FixedCorpus : ICorpusEntries
    {
        private readonly IReadOnlyList<CorpusEntry> _entries;
        public FixedCorpus(IReadOnlyList<CorpusEntry> entries) => _entries = entries;
        public IReadOnlyList<CorpusEntry> List() => _entries;
    }

    private sealed class BrokenModel : ITriageModel
    {
        public ModelDecision Decide(string corpus, string transcript) => throw new InvalidOperationException("model down");
    }

    private sealed class RecordingModel : ITriageModel
    {
        private readonly ModelDecision? _decision;
        public string Corpus { get; private set; } = "";
        public RecordingModel(ModelDecision? decision) => _decision = decision;
        public ModelDecision Decide(string corpus, string transcript)
        {
            Corpus = corpus;
            return _decision ?? throw new InvalidOperationException("no decision");
        }
    }

    private sealed class RecordingEffects : ITriageEffects
    {
        public List<Guid> Qualified { get; } = [];
        public List<Guid> Queues { get; } = [];
        public List<Guid> Armed { get; } = [];
        public List<Guid> EndedFollowUp { get; } = [];
        public List<MetaMirror> Events { get; } = [];
        public void MarkQualified(Guid leadId) => Qualified.Add(leadId);
        public void OpenQueue(Guid leadId) => Queues.Add(leadId);
        public void ArmFollowUp(Guid leadId) => Armed.Add(leadId);
        public void EndFollowUp(Guid leadId) => EndedFollowUp.Add(leadId);
        public void RequestMirror(MetaMirror mirror) => Events.Add(mirror);
    }
}
