using Application.Auth;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WebApiRest.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCitaDentAuthorization();
// Use cases. Task 1b-07 moves these registrations to an AddApplication() extension.
builder.Services.AddScoped<LoginHandler>();

builder.Services.AddHealthChecks().AddDbContextCheck<CitaDentDbContext>("postgres");

var app = builder.Build();

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
