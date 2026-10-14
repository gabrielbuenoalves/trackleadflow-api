namespace TrackLeadFlow.Modules.WhatsApp.Infrastructure.External.CloudApi;

public sealed record OutboundMessage(string Kind, string Body, string? TemplateName);

public static class SendMessage
{
    public static OutboundMessage? Choose(bool insideServiceWindow, bool hasApprovedTemplate, string body, string templateName)
    {
        if (insideServiceWindow)
        {
            return new OutboundMessage("text", body, null);
        }

        if (!hasApprovedTemplate)
        {
            return null;
        }

        return new OutboundMessage("template", body, templateName);
    }
}
