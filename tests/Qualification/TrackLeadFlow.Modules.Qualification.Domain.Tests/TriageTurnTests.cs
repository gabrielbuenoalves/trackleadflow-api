using TrackLeadFlow.Modules.Qualification.Domain;
using Xunit;

namespace TrackLeadFlow.Modules.Qualification.Domain.Tests;

public class TriageTurnTests
{
    private static ModelDecision Decision(
        TriageSituation situation,
        WillingnessToPay willingness,
        string? reply = "resposta") =>
        new(situation, willingness, reply, "Ana", "123");

    [Fact]
    public void In_conversation_keeps_the_queue_closed_and_does_not_arm_follow_up()
    {
        var result = TriageTurn.Apply(
            LeadTriagePhase.InConversation,
            Decision(TriageSituation.InConversation, WillingnessToPay.Unknown));

        Assert.Equal(LeadTriagePhase.InConversation, result.Phase);
        Assert.True(result.SendGeneratedReply);
        Assert.DoesNotContain(TurnSignal.ArmFollowUp, result.Signals);
        Assert.DoesNotContain(TurnSignal.OpenSpecialistQueue, result.Signals);
        Assert.False(TriageTurn.ArmsFollowUpOnSilence(result.Phase));
    }

    [Fact]
    public void Qualified_and_willing_to_pay_opens_the_queue_and_stops_the_ai()
    {
        var result = TriageTurn.Apply(
            LeadTriagePhase.InConversation,
            Decision(TriageSituation.Qualified, WillingnessToPay.Yes));

        Assert.Equal(LeadTriagePhase.WithSpecialist, result.Phase);
        Assert.Contains(TurnSignal.MarkQualified, result.Signals);
        Assert.Contains(TurnSignal.OpenSpecialistQueue, result.Signals);
        Assert.Contains(TurnSignal.StopAi, result.Signals);
        Assert.Contains(TurnSignal.EndFollowUp, result.Signals);
        Assert.False(TriageTurn.ArmsFollowUpOnSilence(result.Phase));
    }

    [Fact]
    public void Qualified_without_willingness_stays_with_the_ai_and_arms_follow_up_on_silence()
    {
        var result = TriageTurn.Apply(
            LeadTriagePhase.InConversation,
            Decision(TriageSituation.Qualified, WillingnessToPay.Unknown));

        Assert.Equal(LeadTriagePhase.Qualified, result.Phase);
        Assert.Contains(TurnSignal.MarkQualified, result.Signals);
        Assert.DoesNotContain(TurnSignal.OpenSpecialistQueue, result.Signals);
        Assert.Contains(TurnSignal.ArmFollowUp, result.Signals);
        Assert.True(TriageTurn.ArmsFollowUpOnSilence(result.Phase));
    }

    [Fact]
    public void Opening_the_queue_does_not_mark_qualified_a_second_time()
    {
        var result = TriageTurn.Apply(
            LeadTriagePhase.Qualified,
            Decision(TriageSituation.Qualified, WillingnessToPay.Yes));

        Assert.Equal(LeadTriagePhase.WithSpecialist, result.Phase);
        Assert.DoesNotContain(TurnSignal.MarkQualified, result.Signals);
        Assert.Contains(TurnSignal.OpenSpecialistQueue, result.Signals);
        Assert.Contains(TurnSignal.EndFollowUp, result.Signals);
    }

    [Fact]
    public void Disqualified_ends_triage_without_a_queue_or_follow_up()
    {
        var result = TriageTurn.Apply(
            LeadTriagePhase.InConversation,
            Decision(TriageSituation.Disqualified, WillingnessToPay.No));

        Assert.Equal(LeadTriagePhase.Disqualified, result.Phase);
        Assert.Contains(TurnSignal.EndTriage, result.Signals);
        Assert.Contains(TurnSignal.EndFollowUp, result.Signals);
        Assert.DoesNotContain(TurnSignal.OpenSpecialistQueue, result.Signals);
        Assert.DoesNotContain(TurnSignal.ArmFollowUp, result.Signals);
        Assert.False(TriageTurn.ArmsFollowUpOnSilence(result.Phase));
    }

    [Fact]
    public void Model_failure_does_not_change_phase_or_send_generated_text()
    {
        var result = TriageTurn.Apply(LeadTriagePhase.InConversation, decision: null);

        Assert.Equal(LeadTriagePhase.InConversation, result.Phase);
        Assert.False(result.SendGeneratedReply);
        Assert.Null(result.ReplyText);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void Specialist_phase_does_not_send_another_ai_reply()
    {
        var result = TriageTurn.Apply(
            LeadTriagePhase.WithSpecialist,
            Decision(TriageSituation.Qualified, WillingnessToPay.Yes));

        Assert.Equal(LeadTriagePhase.WithSpecialist, result.Phase);
        Assert.False(result.SendGeneratedReply);
        Assert.Empty(result.Signals);
    }
}
