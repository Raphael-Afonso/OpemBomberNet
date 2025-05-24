# Use the official .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy the solution file and restore dependencies for all projects
COPY *.sln .
COPY OpenBomberNet.Application/*.csproj ./OpenBomberNet.Application/
COPY OpenBomberNet.Domain/*.csproj ./OpenBomberNet.Domain/
COPY OpenBomberNet.Infrastructure/*.csproj ./OpenBomberNet.Infrastructure/
COPY OpenBomberNet.Server/*.csproj ./OpenBomberNet.Server/
COPY OpenBomberNet.Common/*.csproj ./OpenBomberNet.Common/
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Build the Server project in Release configuration
WORKDIR /source/OpenBomberNet.Server
RUN dotnet publish -c Release -o /app/publish --no-restore

# Use the official .NET runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose the port the server will listen on (adjust if needed)
EXPOSE 8888

# Define the entry point for the container
ENTRYPOINT ["dotnet", "OpenBomberNet.Server.dll"]

