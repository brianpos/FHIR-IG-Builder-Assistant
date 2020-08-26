#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY . .

FROM build AS publish
RUN dotnet publish "FHIR IG Builder Assistant.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /data
COPY --from=publish /app/publish /app

# and the endpoint for the actual builder
ENTRYPOINT ["dotnet", "/app/FHIR_IG_Builder_Assistant.dll"]
