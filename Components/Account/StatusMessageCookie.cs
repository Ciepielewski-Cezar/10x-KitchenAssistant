namespace KitchenAssistant.Components.Account;

internal static class StatusMessageCookie
{
    private const string SuccessPrefix = "success|";
    private const string ErrorPrefix = "error|";

    public static string Encode(string message, StatusKind kind)
        => (kind == StatusKind.Error ? ErrorPrefix : SuccessPrefix) + message;

    public static (string Message, StatusKind? Kind) Decode(string raw)
    {
        // Only the first '|' separates, so the message itself may contain one.
        if (raw.StartsWith(SuccessPrefix, StringComparison.Ordinal))
        {
            return (raw[SuccessPrefix.Length..], StatusKind.Success);
        }

        if (raw.StartsWith(ErrorPrefix, StringComparison.Ordinal))
        {
            return (raw[ErrorPrefix.Length..], StatusKind.Error);
        }

        // Legacy value written by the callers that pass a bare message.
        return (raw, null);
    }

    public static StatusKind Resolve(string message, StatusKind? kind)
        => kind ?? (message.StartsWith("Error", StringComparison.Ordinal) ? StatusKind.Error : StatusKind.Success);
}
