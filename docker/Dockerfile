FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
COPY app/ .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Cocoar.Shelf.dll"]
