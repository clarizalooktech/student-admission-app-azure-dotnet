using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using AdmissionAgent.Services;

var builder = WebApplication.CreateBuilder(args);

// ── OpenAI client ──────────────────────────────────────────────────────────
var openAiEndpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]!;
var openAiKey      = builder.Configuration["AZURE_OPENAI_KEY"]!;
var openAiModel    = builder.Configuration["AZURE_OPENAI_MODEL"] ?? "gpt-4o-mini";

builder.Services.AddSingleton(new OpenAIClient(
    new Uri(openAiEndpoint),
    new Azure.AzureKeyCredential(openAiKey)));

builder.Services.AddSingleton(sp =>
    new AgentConfig(Model: openAiModel));

builder.Services.AddScoped<AdmissionAgentService>();

// ── Observability ──────────────────────────────────────────────────────────
// App Insights (traces + exceptions)
var appInsightsKey = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsKey))
    builder.Services.AddOpenTelemetry().UseAzureMonitor();

// Prometheus (agent step metrics)
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddMeter("AdmissionAgent")
        .AddPrometheusExporter());

// ── Web API ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()));

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.MapPrometheusScrapingEndpoint(); // /metrics — Prometheus scrapes this

app.Run();
