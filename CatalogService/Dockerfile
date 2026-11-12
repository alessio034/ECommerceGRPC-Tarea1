# =====================================================================
# CatalogService/Dockerfile · CONTEXTO CATÁLOGO · Semana 4 (S4-02)
#
# La RECETA de la imagen de Catálogo. Se construye desde la RAÍZ de la
# solución, no desde esta carpeta, porque Catálogo usa el proyecto
# ECommerce.Contracts y Docker solo ve lo que está dentro del contexto:
#
#     docker build -t catalog-service:s4 -f CatalogService/Dockerfile .
#                                                                     ^ el contexto: la raíz
#
# Cuatro etapas (multi-etapa): base · build · publish · final.
# Solo la última es la imagen que corre: lleva el runtime y lo publicado,
# no el SDK con el que se compiló.
#
# S4-03: el Dockerfile de Usuarios y el de Pedidos se hacen COPIANDO este
# archivo. Cambian TRES líneas, marcadas abajo con «CAMBIA».
# =====================================================================

# ---- Etapa 1 · base: el runtime de ASP.NET Core 8 (sin compilador) -------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
# Adentro del contenedor, .NET 8 escucha en el puerto 8080.
# Afuera, docker-compose.yml lo publica como 5001 ("5001:8080").
EXPOSE 8080
# curl: lo usan el healthcheck (S4-13) y la prueba de red por nombre (S4-08)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# ---- Etapa 2 · build: el SDK completo, solo para compilar ----------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Primero SOLO los .csproj y el restore. Mientras las dependencias no
# cambien, Docker reutiliza esta capa (caché) y el build tarda segundos.
COPY ["ECommerce.Contracts/ECommerce.Contracts.csproj", "ECommerce.Contracts/"]
# CAMBIA (1 de 3): la carpeta y el .csproj del servicio
COPY ["CatalogService/CatalogService.csproj", "CatalogService/"]
# CAMBIA (2 de 3): la carpeta de trabajo del servicio
WORKDIR /src/CatalogService
RUN dotnet restore
# Ahora sí, todo el código (lo que .dockerignore deja pasar)
COPY . /src/
RUN dotnet build -c Release -o /app/build

# ---- Etapa 3 · publish: deja solo lo necesario para correr --------------
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ---- Etapa 4 · final: runtime + lo publicado. ESTA es la imagen ----------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# CAMBIA (3 de 3): el .dll que se ejecuta
ENTRYPOINT ["dotnet", "CatalogService.dll"]
