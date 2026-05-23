using FluentValidation;
using Links.Api.Common;
using Links.Api.Modules.Auth;
using Links.Api.Modules.Auth.Endpoints;
using Links.Api.Modules.Users;
using Links.Api.Modules.Users.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Modules
builder.Services.AddAuthModule(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddScoped<UserService>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Health
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Endpoints
app.MapAuthEndpoints();
app.MapUsersEndpoints();

app.Run();
