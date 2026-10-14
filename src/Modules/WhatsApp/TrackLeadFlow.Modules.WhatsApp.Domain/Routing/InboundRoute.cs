namespace TrackLeadFlow.Modules.WhatsApp.Domain;

public static class PhoneNumber
{
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        return new string(phone.Where(char.IsDigit).ToArray());
    }
}

public enum InboundLane
{
    Lead,
    Operator,
}

public static class InboundRoute
{
    public static InboundLane Resolve(string senderPhone, IEnumerable<string> operatorPhones)
    {
        var sender = PhoneNumber.Normalize(senderPhone);
        if (sender.Length == 0)
        {
            return InboundLane.Lead;
        }

        foreach (var phone in operatorPhones)
        {
            if (PhoneNumber.Normalize(phone) == sender)
            {
                return InboundLane.Operator;
            }
        }

        return InboundLane.Lead;
    }
}
