using Api;
using Api.Extensions;
using Api.Middleware;
using Application;
using Infrastructure;
using Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddApiCors(builder.Configuration);

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Configuration);

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();

    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseApiCors();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
