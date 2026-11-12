# ProductService.gRPC/Dockerfile · se construye desde ESTA carpeta (context: ../ProductService.gRPC)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ProductService.gRPC.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 7001
ENTRYPOINT ["dotnet", "ProductService.gRPC.dll"]
