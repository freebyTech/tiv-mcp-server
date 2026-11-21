FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

ARG BUILD_VERSION

# copy csproj files of projects and restore as distinct layers so we break cache as little as possible
COPY ./tivMcp.Api/*.csproj ./tivMcp.Api/
COPY ./tivMcp.Api.Tests/*.csproj ./tivMcp.Api.Tests/

WORKDIR /app/tivMcp.Api/
RUN dotnet restore
WORKDIR /app/tivMcp.Api.Tests/
RUN dotnet restore

# copy everything else and build app
WORKDIR /app/
COPY ./tivMcp.Api.sln ./
COPY ./tivMcp.Api/. ./tivMcp.Api/
COPY ./tivMcp.Api.Tests/. ./tivMcp.Api.Tests/

ENV BUILD_VERSION=${BUILD_VERSION}

# run unit tests, a failing test fails the image build
WORKDIR /app/tivMcp.Api.Tests/
RUN dotnet test

WORKDIR /app/
RUN dotnet publish ./tivMcp.Api/tivMcp.Api.csproj -c Release -o /app/tiv-mcp-budget-pub

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/tiv-mcp-budget-pub ./
ENTRYPOINT ["dotnet", "tivMcp.Api.dll"]
