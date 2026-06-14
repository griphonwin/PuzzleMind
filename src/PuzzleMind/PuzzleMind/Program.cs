using Microsoft.AspNetCore.Localization;
using PuzzleMind;
using PuzzleMind.Services;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRadzenComponents();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<ClipboardService>();
builder.Services.AddScoped<MahjongAnalyzer>();
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        // Увеличиваем лимит сообщения до 10 МБ (хватит для любого скриншота)
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    });

// 1. Регистрируем службы локализации
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// 2. Настраиваем поддерживаемые культуры (базовый - en)
var supportedCultures = new[] { "en", "ru", "fr", "de", "th", "vi", "ja" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0]) // en
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

var app = builder.Build();

app.UseRequestLocalization(localizationOptions);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Эндпоинт для переключения языка и записи Cookies
app.MapGet("/Culture/SetCulture", (string culture, string redirectUri, HttpContext httpContext) =>
{
    if (culture != null)
    {
        // Записываем куку локализации, которая будет жить 1 год
        httpContext.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
        );
    }

    // Возвращаем пользователя обратно на ту страницу, где он находился
    return Results.Redirect(redirectUri);
});

app.Run();
