using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
var app = builder.Build();
app.UseCors();

var users = new Dictionary<string, (string Password, string Role)>(StringComparer.OrdinalIgnoreCase)
{
    ["admin@office.test"] = ("admin", "Admin"),
    ["specialist@office.test"] = ("specialist", "Specialist"),
};
var sessions = new ConcurrentDictionary<string, string>();
var settings = new ConcurrentDictionary<string, Dictionary<string, string>>();
var queue = new[]
{
    new WorkItem("lead-1", "Ana", "Queued"),
    new WorkItem("lead-2", "Bruno", "Assigned"),
};

app.MapPost("/identity/login", (LoginRequest request) =>
{
    if (!users.TryGetValue(request.Email, out var user) || user.Password != request.Password)
    {
        return Results.Unauthorized();
    }

    var token = Guid.NewGuid().ToString("N");
    sessions[token] = user.Role;
    return Results.Ok(new { token, role = user.Role });
});

app.MapGet("/attendance/work-items", (HttpRequest request) =>
{
    if (!Known(request, sessions))
    {
        return Results.Unauthorized();
    }

    var visible = queue.Where(item => item.Status is "Queued" or "Assigned");
    return Results.Ok(visible);
});

MapSetting(app, sessions, settings, "/qualification/corpus", secretKeys: []);
MapSetting(app, sessions, settings, "/identity/specialist-phone", secretKeys: []);
MapSetting(app, sessions, settings, "/followup/sequences", secretKeys: []);
MapSetting(app, sessions, settings, "/payments/labels", secretKeys: []);
MapSetting(app, sessions, settings, "/payments/gateway", secretKeys: ["secret"]);
MapSetting(app, sessions, settings, "/whatsapp/channel", secretKeys: ["token"]);
MapSetting(app, sessions, settings, "/whatsapp/templates", secretKeys: []);
MapSetting(app, sessions, settings, "/marketing/dataset", secretKeys: ["token"]);

app.MapGet("/qualification/leads/{id}", (string id, HttpRequest request) =>
    Known(request, sessions) ? Results.Ok(new { id, phase = "WithSpecialist", name = id == "lead-1" ? "Ana" : "Bruno" }) : Results.Unauthorized());
app.MapGet("/whatsapp/leads/{id}/messages", (string id, HttpRequest request) =>
    Known(request, sessions) ? Results.Ok(new[] { new { id, body = "Oi, quero a análise." } }) : Results.Unauthorized());
app.MapGet("/followup/leads/{id}", (string id, HttpRequest request) =>
    Known(request, sessions) ? Results.Ok(new { id, status = "Cancelled" }) : Results.Unauthorized());
app.MapGet("/payments/leads/{id}/charges", (string id, HttpRequest request) =>
    Known(request, sessions) ? Results.Ok(new[] { new { id, label = "Análise", status = "Pending" } }) : Results.Unauthorized());
app.MapGet("/marketing/leads/{id}/deliveries", (string id, HttpRequest request) =>
    Known(request, sessions) ? Results.Ok(new[] { new { id, businessEvent = "QualifiedLead", status = "Pending" } }) : Results.Unauthorized());

app.Run("http://127.0.0.1:5080");

static bool Known(HttpRequest request, ConcurrentDictionary<string, string> sessions)
{
    var header = request.Headers.Authorization.ToString();
    return header.StartsWith("Bearer ", StringComparison.Ordinal) && sessions.ContainsKey(header["Bearer ".Length..]);
}

static void MapSetting(WebApplication app, ConcurrentDictionary<string, string> sessions, ConcurrentDictionary<string, Dictionary<string, string>> settings, string path, string[] secretKeys)
{
    app.MapGet(path, (HttpRequest request) =>
    {
        if (!Known(request, sessions))
        {
            return Results.Unauthorized();
        }

        settings.TryGetValue(path, out var stored);
        stored ??= [];
        var visible = stored.Where(pair => !secretKeys.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var secret in secretKeys)
        {
            visible[secret + "Configured"] = stored.ContainsKey(secret) ? "true" : "false";
        }

        return Results.Ok(visible);
    });

    app.MapPut(path, async (HttpRequest request) =>
    {
        if (!Known(request, sessions))
        {
            return Results.Unauthorized();
        }

        var body = await request.ReadFromJsonAsync<Dictionary<string, string>>() ?? [];
        if (!settings.TryGetValue(path, out var current))
        {
            current = [];
        }

        foreach (var pair in body)
        {
            if (secretKeys.Contains(pair.Key) && string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            current[pair.Key] = pair.Value;
        }

        settings[path] = current;
        return Results.NoContent();
    });
}

internal sealed record LoginRequest(string Email, string Password);
internal sealed record WorkItem(string Id, string Name, string Status);

public partial class Program;
