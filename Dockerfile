FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Links.Api/Links.Api.csproj", "Links.Api/"]
RUN dotnet restore "Links.Api/Links.Api.csproj"
COPY src/ .
RUN dotnet publish "Links.Api/Links.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
# Data Protection key ring — mount a persistent volume at /app/keys
# to keep MFA secret encryption keys stable across restarts.
# Example: docker run -v keys-volume:/app/keys ...
# Use an ephemeral filesystem or bind-mount for production.
ENV DataProtection__KeyDirectory=/app/keys
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Links.Api.dll"]
