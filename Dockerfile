FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY ["Directory.Build.props", "Directory.Build.props"]
COPY ["src/AzureBlobSearch.Application/AzureBlobSearch.Application.csproj", "src/AzureBlobSearch.Application/"]
COPY ["src/AzureBlobSearch.Infrastructure/AzureBlobSearch.Infrastructure.csproj", "src/AzureBlobSearch.Infrastructure/"]
COPY ["src/AzureBlobSearch.Api/AzureBlobSearch.Api.csproj", "src/AzureBlobSearch.Api/"]
RUN dotnet restore "src/AzureBlobSearch.Api/AzureBlobSearch.Api.csproj"

COPY . .
RUN dotnet publish "src/AzureBlobSearch.Api/AzureBlobSearch.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "AzureBlobSearch.Api.dll"]
