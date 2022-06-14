FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src
COPY ["Microsoft.ContainerApps.TestApps.CrossFeatureApp.Client.csproj", "."]
RUN dotnet restore "./Microsoft.ContainerApps.TestApps.CrossFeatureApp.Client.csproj"
COPY . .
WORKDIR /src
RUN dotnet build "Microsoft.ContainerApps.TestApps.CrossFeatureApp.Client.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Microsoft.ContainerApps.TestApps.CrossFeatureApp.Client.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.ContainerApps.TestApps.CrossFeatureApp.Client.dll"]
