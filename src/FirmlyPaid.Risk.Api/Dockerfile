# Build and run FirmlyPaid.Risk.Api. Context is the repository root so central package
# management (Directory.Packages.props) and FirmlyPaid.Shared are both visible.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/FirmlyPaid.Shared/ src/FirmlyPaid.Shared/
COPY src/FirmlyPaid.Risk.Api/ src/FirmlyPaid.Risk.Api/

RUN dotnet publish src/FirmlyPaid.Risk.Api/FirmlyPaid.Risk.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./

# Containers speak plain HTTP on the compose network; TLS terminates at the Gateway (step 7).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the built-in non-root user.
USER $APP_UID

ENTRYPOINT ["dotnet", "FirmlyPaid.Risk.Api.dll"]
