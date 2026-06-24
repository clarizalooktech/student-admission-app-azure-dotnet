using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using AdmissionAgent.Services;

var builder = WebApplication.CreateBuilder(args);

// ── OpenAI client ──────────────────────────────────────────────────────────
var openAiEndpoint  = builder.Configuration["AZURE_OPENAI_ENDPOINT"]!;
var openAiKey       = builder.Configuration["AZURE_OPENAI_KEY"]!;
var openAiModel     = builder.Configuration["AZURE_OPENAI_MODEL"] ?? "gpt-4o";
var assistantId     = builder.Configuration["AZURE_OPENAI_ASSISTANT_ID"]!;

// SDK v2.x uses AzureOpenAIClient
builder.Services.AddSingleton(new AzureOpenAIClient(
    new Uri(openAiEndpoint),
    new Azure.AzureKeyCredential(openAiKey)));

builder.Services.AddSingleton(sp =>
    new AgentConfig(Model: openAiModel, AssistantId: assistantId));

builder.Services.AddScoped<AdmissionAgentService>();

// ── Observability ──────────────────────────────────────────────────────────
var appInsightsKey = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsKey))
    builder.Services.AddOpenTelemetry().UseAzureMonitor();

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
app.MapPrometheusScrapingEndpoint();

app.Run();