using Microsoft.JSInterop;

namespace PuzzleMind.Services;

public class ClipboardService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<byte[]?> GetImageAsync()
    {
        // Ленивая загрузка JS модуля
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/clipboard.js");

        return await _module.InvokeAsync<byte[]?>("getImageFromClipboard");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}