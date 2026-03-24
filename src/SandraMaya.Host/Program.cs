using SandraMaya.Host.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSandraMayaHost(builder.Configuration);

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new
{
    service = "SandraMaya.Host",
    status = "ok",
    endpoints = new
    {
        health = "/health"
    }
}));

app.Run();

public partial class Program;
