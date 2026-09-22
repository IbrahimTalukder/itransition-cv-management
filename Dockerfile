FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src


COPY ["CvManagementSystem.csproj", "./"]
RUN dotnet restore "CvManagementSystem.csproj"


COPY . .
RUN dotnet publish "CvManagementSystem.csproj" -c Release -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CvManagementSystem.dll"]