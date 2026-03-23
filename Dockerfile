FROM node:22-alpine AS client-build
WORKDIR /client
COPY src/Cocoar.Shelf.Client/package.json src/Cocoar.Shelf.Client/package-lock.json ./
RUN npm ci
COPY src/Cocoar.Shelf.Client/ .
RUN npx vite build --outDir /client/dist

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Cocoar.Shelf/Cocoar.Shelf.csproj Cocoar.Shelf/
RUN dotnet restore Cocoar.Shelf/Cocoar.Shelf.csproj

COPY src/ .
COPY --from=client-build /client/dist/ Cocoar.Shelf/wwwroot/
RUN dotnet publish Cocoar.Shelf/Cocoar.Shelf.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Cocoar.Shelf.dll"]
