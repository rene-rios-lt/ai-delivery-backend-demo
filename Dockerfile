FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ServiceRequest.sln .
COPY src/ServiceRequest.Domain/ServiceRequest.Domain.csproj src/ServiceRequest.Domain/
COPY src/ServiceRequest.Application/ServiceRequest.Application.csproj src/ServiceRequest.Application/
COPY src/ServiceRequest.Infrastructure/ServiceRequest.Infrastructure.csproj src/ServiceRequest.Infrastructure/
COPY src/ServiceRequest.Api/ServiceRequest.Api.csproj src/ServiceRequest.Api/
RUN dotnet restore src/ServiceRequest.Api/ServiceRequest.Api.csproj

COPY . .
RUN dotnet publish src/ServiceRequest.Api/ServiceRequest.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceRequest.Api.dll"]
