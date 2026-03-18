FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Cocoar.Shelf/Cocoar.Shelf.csproj Cocoar.Shelf/
RUN dotnet restore Cocoar.Shelf/Cocoar.Shelf.csproj

COPY src/ .
RUN dotnet publish Cocoar.Shelf/Cocoar.Shelf.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Cocoar.Shelf.dll"]
