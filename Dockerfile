# Stage 1: Build React 19 Frontend
FROM node:20-alpine AS web-build
WORKDIR /app

COPY src/NiftyQuant.Web/package*.json ./
RUN npm install

COPY src/NiftyQuant.Web/ ./
RUN npm run build

# Stage 2: Build .NET 10 LTS Backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/NiftyQuant.Domain/NiftyQuant.Domain.csproj", "src/NiftyQuant.Domain/"]
COPY ["src/NiftyQuant.Application/NiftyQuant.Application.csproj", "src/NiftyQuant.Application/"]
COPY ["src/NiftyQuant.Infrastructure/NiftyQuant.Infrastructure.csproj", "src/NiftyQuant.Infrastructure/"]
COPY ["src/NiftyQuant.Api/NiftyQuant.Api.csproj", "src/NiftyQuant.Api/"]

RUN dotnet restore "src/NiftyQuant.Api/NiftyQuant.Api.csproj"

COPY . .
WORKDIR "/src/src/NiftyQuant.Api"
RUN dotnet publish "NiftyQuant.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Unified Production Runtime Container
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=web-build /app/dist ./wwwroot

ENV DatabaseProvider="Sqlite"
ENV ConnectionStrings__DefaultConnection="Data Source=niftyquant.db"
ENV ASPNETCORE_URLS="http://+:5000"
EXPOSE 5000

ENTRYPOINT ["dotnet", "NiftyQuant.Api.dll"]
