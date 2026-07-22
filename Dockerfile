# Backend (ASP.NET Core minimal API) — deployed on Render.
# Build context is the repo root (see .dockerignore) so paths below are backend/...
# Local dev does not use this file — see CLAUDE.md for the dotnet run instructions.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/CodeReviewAI.Api/CodeReviewAI.Api.csproj backend/CodeReviewAI.Api/
RUN dotnet restore backend/CodeReviewAI.Api/CodeReviewAI.Api.csproj

COPY backend/CodeReviewAI.Api/ backend/CodeReviewAI.Api/
RUN dotnet publish backend/CodeReviewAI.Api/CodeReviewAI.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "CodeReviewAI.Api.dll"]
