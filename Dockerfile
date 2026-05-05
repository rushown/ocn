# ============================================================
# Stage 1: Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and restore
COPY *.sln .
COPY src/EWallet.Domain/*.csproj src/EWallet.Domain/
COPY src/EWallet.Application/*.csproj src/EWallet.Application/
COPY src/EWallet.Infrastructure/*.csproj src/EWallet.Infrastructure/
COPY src/EWallet.API/*.csproj src/EWallet.API/
COPY src/EWallet.BlazorClient/*.csproj src/EWallet.BlazorClient/
RUN dotnet restore

# Copy everything and build
COPY . .
RUN dotnet build -c Release --no-restore

# ============================================================
# Stage 2: Publish API
# ============================================================
FROM build AS publish-api
RUN dotnet publish src/EWallet.API/EWallet.API.csproj -c Release -o /app/api --no-build

# ============================================================
# Stage 3: Publish Blazor
# ============================================================
FROM build AS publish-blazor
RUN dotnet publish src/EWallet.BlazorClient/EWallet.BlazorClient.csproj -c Release -o /app/blazor --no-build

# ============================================================
# Stage 4: API Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
COPY --from=publish-api /app/api .
EXPOSE 8080
ENTRYPOINT ["dotnet", "EWallet.API.dll"]

# ============================================================
# Stage 5: Blazor Runtime (nginx)
# ============================================================
FROM nginx:alpine AS blazor
COPY --from=publish-blazor /app/blazor/wwwroot /usr/share/nginx/html
COPY src/EWallet.BlazorClient/nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
