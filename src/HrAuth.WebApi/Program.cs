using HrAuth.Application;
using HrAuth.Infrastructure;
using HrAuth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

// Npgsql: accept DateTime values regardless of Kind (legacy timestamp mapping).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Presentation ──────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "HrAuth API",
        Version = "v1",
        Description = "Clean Architecture wrapper over the HaramAPI HrAuth "
                      + "login / refresh-token / validate-token endpoints.",
    });
});

// ── Clean Architecture layers ─────────────────────────────────
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Apply database migrations on startup (best-effort — the app still runs if
// PostgreSQL is unavailable; persistence is simply disabled for that run).
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        scope.ServiceProvider.GetRequiredService<HrAuthDbContext>()
            .Database.Migrate();
        logger.LogInformation("Database is up to date.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex,
            "Database unavailable — persistence disabled for this run.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "HrAuth API v1");
        options.RoutePrefix = "swagger"; // Swagger UI lives at /swagger.
    });
}

// Serve the front-end (login + landing page) from wwwroot; index.html at "/".
// No-cache headers so the browser always revalidates the SPA assets — edits to
// the HTML/JS are picked up on the next refresh instead of being served stale.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "0";
    },
});

// Redirect to HTTPS only outside Development. Locally the SPA is commonly
// opened over http://localhost:5080; a 307 to the https port is cross-origin,
// and browsers strip the Authorization header on that redirect — so every
// authenticated API call would silently lose its bearer token.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.MapControllers();

app.Run();
