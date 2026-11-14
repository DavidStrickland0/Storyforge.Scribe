using System.Text;
using StoryForge.Scribe.Components;
using StoryForge.Scribe.Domain;
using StoryForge.Scribe.Services;
using StoryForge.Scribe.Services.Ai;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var storageOptions = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<AppPaths>();
builder.Services.AddSingleton<BookStore>();
builder.Services.AddSingleton<AiSettingsStore>();
builder.Services.AddSingleton<AiAssistant>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddHttpClient("ai", client =>
{
    client.Timeout = TimeSpan.FromMinutes(4);
});

builder.Services.AddHostedService<AutosaveService>();
builder.Services.AddHostedService<BackupService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapGet("/export/manuscript.md", (BookStore store) =>
{
    var book = store.Book;
    var bytes = Encoding.UTF8.GetBytes(store.ExportMarkdown());
    return Results.File(bytes, "text/markdown; charset=utf-8", $"{ManuscriptExporter.Slug(book.Title)}.md");
});

app.MapGet("/export/book.json", (BookStore store) =>
{
    var book = store.Book;
    var bytes = Encoding.UTF8.GetBytes(store.ExportJson());
    return Results.File(bytes, "application/json; charset=utf-8", $"{ManuscriptExporter.Slug(book.Title)}.json");
});

app.MapGet("/health", (BookStore store) => Results.Json(new
{
    status = "ok",
    book = store.Book.Title,
    words = Outline.BookWords(store.Book),
    saveState = store.State.ToString(),
    lastSavedUtc = store.LastSavedUtc,
    lastBackupUtc = store.LastBackupUtc
}));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
