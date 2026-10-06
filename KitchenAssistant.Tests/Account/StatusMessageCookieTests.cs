using KitchenAssistant.Components.Account;

namespace KitchenAssistant.Tests.Account;

public class StatusMessageCookieTests
{
    [Theory]
    [InlineData("Nie udało się zapisać.", StatusKind.Error)]
    [InlineData("Zapisano zmiany.", StatusKind.Success)]
    public void Encode_then_decode_round_trips_the_message_and_kind(string message, StatusKind kind)
    {
        var (decodedMessage, decodedKind) = StatusMessageCookie.Decode(StatusMessageCookie.Encode(message, kind));

        Assert.Equal(message, decodedMessage);
        Assert.Equal(kind, decodedKind);
    }

    [Fact]
    public void Encode_prefixes_the_kind()
    {
        Assert.Equal("success|Zapisano zmiany.", StatusMessageCookie.Encode("Zapisano zmiany.", StatusKind.Success));
        Assert.Equal("error|Nie udało się zapisać.", StatusMessageCookie.Encode("Nie udało się zapisać.", StatusKind.Error));
    }

    [Fact]
    public void Message_containing_a_pipe_round_trips()
    {
        var (message, kind) = StatusMessageCookie.Decode(StatusMessageCookie.Encode("Pole a|b jest niepoprawne.", StatusKind.Error));

        Assert.Equal("Pole a|b jest niepoprawne.", message);
        Assert.Equal(StatusKind.Error, kind);
    }

    [Fact]
    public void Legacy_error_value_decodes_whole_with_no_kind_and_resolves_to_error()
    {
        var (message, kind) = StatusMessageCookie.Decode("Error: Failed to set phone number.");

        Assert.Equal("Error: Failed to set phone number.", message);
        Assert.Null(kind);
        Assert.Equal(StatusKind.Error, StatusMessageCookie.Resolve(message, kind));
    }

    [Fact]
    public void Legacy_value_without_the_error_prefix_resolves_to_success()
    {
        var (message, kind) = StatusMessageCookie.Decode("Your profile has been updated");

        Assert.Equal("Your profile has been updated", message);
        Assert.Null(kind);
        Assert.Equal(StatusKind.Success, StatusMessageCookie.Resolve(message, kind));
    }

    [Fact]
    public void Explicit_kind_beats_the_text()
    {
        Assert.Equal(StatusKind.Success, StatusMessageCookie.Resolve("Error: x", StatusKind.Success));
        Assert.Equal(StatusKind.Error, StatusMessageCookie.Resolve("Błąd zapisu", StatusKind.Error));
    }

    [Fact]
    public void Unknown_prefix_is_treated_as_legacy_text()
    {
        var (message, kind) = StatusMessageCookie.Decode("info|x");

        Assert.Equal("info|x", message);
        Assert.Null(kind);
    }

    [Fact]
    public void Empty_string_decodes_without_throwing()
    {
        var (message, kind) = StatusMessageCookie.Decode("");

        Assert.Equal("", message);
        Assert.Null(kind);
    }
}
