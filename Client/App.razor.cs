using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Client;

/// <summary>
/// Code-behind de <see cref="App"/>: arranque de la sesión y retirada de la
/// pantalla de carga.
/// </summary>
public partial class App
{
    private bool ready;
    private bool bootHidden;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // The session lives in the cookie that the server sets: asking the
            // API who we are is enough to know if we are signed in. It is done
            // before the router is mounted so every page already knows if
            // there is a session.
            await Auth.InitializeAsync();
        }
        catch
        {
            /* without a known session the app simply starts without it */
        }

        ready = true;
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ready || bootHidden) return;

        // The loading screen is outside #app, so it can fade away when Blazor
        // takes over instead of vanishing in a flash.
        bootHidden = true;

        try
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/boot.js?v=1");
            await module.InvokeVoidAsync("hideBoot");
            await module.DisposeAsync();
        }
        catch
        {
            /* if the screen cannot be removed, it stays out of the way */
        }
    }
}
