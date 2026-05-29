FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CryptoArbitrage.Api.csproj", "./"]
RUN dotnet restore "CryptoArbitrage.Api.csproj"
COPY . .
RUN dotnet publish "CryptoArbitrage.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CryptoArbitrage.Api.dll"]