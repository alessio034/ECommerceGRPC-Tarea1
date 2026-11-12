# UserService.gRPC/Dockerfile · se construye desde ESTA carpeta (context: ../UserService.gRPC)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY UserService.gRPC.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 7002
ENTRYPOINT ["dotnet", "UserService.gRPC.dll"]
