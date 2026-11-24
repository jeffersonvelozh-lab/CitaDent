using Application.Auth;
using Application.Users;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WebApiRest.Errors;
using WebApiRest.Security;
using WebApiRest.Startup;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCitaDentAuthorization();
builder.Services.AddCitaDentErrorHandling();
// Use cases. Task 1b-07 moves these registrations to an AddApplication() extension.
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<SeedInitialAdminHandler>();

builder.Services.AddHealthChecks().AddDbContextCheck<CitaDentDbContext>("postgres");

var app = builder.Build();

await app.SeedInitialAdminAsync();

// First in the pipeline, so it catches exceptions from everything after it.
app.UseExceptionHandler();

app.MapHealthChecks("/health").AllowAnonymous();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Lets WebApplicationFactory<Program> find this entry point in the API tests.
public partial class Program;
