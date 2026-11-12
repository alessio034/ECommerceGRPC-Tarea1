# OrderService.gRPC/Dockerfile · se construye desde ESTA carpeta (context: ../OrderService.gRPC)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY OrderService.gRPC.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 7003
ENTRYPOINT ["dotnet", "OrderService.gRPC.dll"]
