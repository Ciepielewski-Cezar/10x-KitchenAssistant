using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace KitchenAssistant.Components.Ui;

public static class ElementFocus
{
    // Focus is a convenience: a render that removed the element, or a dropped circuit, must not crash the page.
    public static async Task TryFocusAsync(ElementReference element)
    {
        try
        {
            await element.FocusAsync();
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
