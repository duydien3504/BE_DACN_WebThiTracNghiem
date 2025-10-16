# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore API_ThiTracNghiem/API_ThiTracNghiem.csproj
RUN dotnet publish API_ThiTracNghiem/API_ThiTracNghiem.csproj -c Release -o /app/publish

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
CMD ["sh","-c","ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet API_ThiTracNghiem.dll"]


