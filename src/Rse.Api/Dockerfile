FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json RecurrenceScheduleEngine.sln ./
COPY src/Rse.Engine/Rse.Engine.csproj src/Rse.Engine/
COPY src/Rse.Api/Rse.Api.csproj src/Rse.Api/
RUN dotnet restore src/Rse.Api/Rse.Api.csproj
COPY src/Rse.Engine/ src/Rse.Engine/
COPY src/Rse.Api/ src/Rse.Api/
RUN dotnet publish src/Rse.Api/Rse.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./
COPY data/clinic_schedule.json data/
ENTRYPOINT ["dotnet", "Rse.Api.dll"]
