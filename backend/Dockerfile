FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/AdmissionAgent/AdmissionAgent.csproj", "AdmissionAgent/"]
RUN dotnet restore "AdmissionAgent/AdmissionAgent.csproj"
COPY src/AdmissionAgent/ AdmissionAgent/
WORKDIR "/src/AdmissionAgent"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AdmissionAgent.dll"]
