using System.Text.Json.Serialization;
using Application;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WebApiRest.Errors;
using WebApiRest.Security;
using WebApiRest.Startup;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Enums travel as text ("Cedula", "Passport"), not numbers; a number or an unknown name is a 400.
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCitaDentAuthorization();
builder.Services.AddCitaDentErrorHandling();
builder.Services.AddApplication();

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
