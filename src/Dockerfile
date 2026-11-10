#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Alpine images keep the runtime image small; the app runs as the image's non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Giacom.Orders.WebApi/Giacom.Orders.WebApi.csproj", "Giacom.Orders.WebApi/"]
COPY ["Giacom.Orders.Application/Giacom.Orders.Application.csproj", "Giacom.Orders.Application/"]
COPY ["Giacom.Orders.Domain/Giacom.Orders.Domain.csproj", "Giacom.Orders.Domain/"]
COPY ["Giacom.Orders.Infrastructure/Giacom.Orders.Infrastructure.csproj", "Giacom.Orders.Infrastructure/"]
RUN dotnet restore "Giacom.Orders.WebApi/Giacom.Orders.WebApi.csproj"
COPY . .
WORKDIR "/src/Giacom.Orders.WebApi"
RUN dotnet build "Giacom.Orders.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Giacom.Orders.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Giacom.Orders.WebApi.dll"]