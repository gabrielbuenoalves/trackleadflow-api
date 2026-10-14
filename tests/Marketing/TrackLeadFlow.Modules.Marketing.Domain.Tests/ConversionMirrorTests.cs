using TrackLeadFlow.Modules.Marketing.Domain;
using Xunit;

namespace TrackLeadFlow.Modules.Marketing.Domain.Tests;

public class ConversionMirrorTests
{
    [Fact]
    public void First_message_with_click_id_sends_lead_submitted_once()
    {
        var first = ConversionMirror.Contact("click-1", "conversation-1", alreadySent: false);
        var second = ConversionMirror.Contact("click-1", "conversation-1", alreadySent: true);

        Assert.NotNull(first);
        Assert.Equal(MetaEventName.LeadSubmitted, first.Name);
        Assert.Equal("conversation-1", first.SourceId);
        Assert.Null(second);
    }

    [Fact]
    public void Contact_without_click_id_is_not_sent()
    {
        Assert.Null(ConversionMirror.Contact(null, "conversation-1", alreadySent: false));
    }

    [Fact]
    public void First_qualified_mark_sends_qualified_lead_once()
    {
        var first = ConversionMirror.Qualified("click-1", "lead-1", alreadySent: false);
        var again = ConversionMirror.Qualified("click-1", "lead-1", alreadySent: true);

        Assert.NotNull(first);
        Assert.Equal(MetaEventName.QualifiedLead, first.Name);
        Assert.Equal("lead-1", first.SourceId);
        Assert.Null(again);
    }

    [Fact]
    public void Qualified_without_click_id_is_not_sent()
    {
        Assert.Null(ConversionMirror.Qualified(" ", "lead-1", alreadySent: false));
    }

    [Fact]
    public void Queue_and_disqualified_do_not_send_an_event()
    {
        Assert.Null(ConversionMirror.QueueOpened());
        Assert.Null(ConversionMirror.Disqualified());
    }

    [Fact]
    public void Each_confirmed_charge_sends_purchase_with_value_and_currency()
    {
        var analysis = ConversionMirror.Purchase("click-1", "charge-1", 150m, "BRL", alreadySent: false);
        var purchase = ConversionMirror.Purchase("click-1", "charge-2", 2000m, "BRL", alreadySent: false);
        var repeated = ConversionMirror.Purchase("click-1", "charge-1", 150m, "BRL", alreadySent: true);

        Assert.Equal(MetaEventName.Purchase, analysis!.Name);
        Assert.Equal(150m, analysis.Value);
        Assert.Equal("BRL", analysis.Currency);
        Assert.Equal("charge-2", purchase!.SourceId);
        Assert.Null(repeated);
    }

    [Fact]
    public void Purchase_without_click_id_is_not_sent()
    {
        Assert.Null(ConversionMirror.Purchase(null, "charge-1", 150m, "BRL", alreadySent: false));
    }
}
