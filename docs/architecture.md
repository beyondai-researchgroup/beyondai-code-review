# Architecture Overview

## Summary

Code Review AI Assistant is a web application that fetches GitHub Pull Request data and streams an AI-generated educational analysis to the user. The developer always makes the final approve/reject decision — the AI only explains and evaluates.

## Components

### Backend — ASP.NET Core 8 (`/backend`)
- Minimal API style, no controllers
- Receives a GitHub PR URL from the frontend
- Uses **Octokit.net** (read-only PAT) to fetch PR metadata, file diffs, and commit messages
- Sends the diff + context to the **Anthropic Claude API** (`claude-sonnet-4-6`) with a system prompt that forbids approve/reject recommendations
- Streams the response back to the frontend via **Server-Sent Events (SSE)**
- Configuration lives in `appsettings.json`; secrets supplied via environment variables (never committed)

### Frontend — Angular 17+ (`/frontend`)
- Standalone components, Signals for state management
- Single-page: PR URL input → streaming analysis view
- Renders AI response as Markdown using **ngx-markdown** + **Prism.js** for code highlighting
- Connects to the backend SSE endpoint and appends tokens as they arrive

### AI Prompt Constraint
The system prompt explicitly instructs Claude to explain what the PR does and assess coding-practice alignment, but **never** to recommend approval or rejection. Any UI copy and prompt changes must preserve this invariant.

## Data Flow

```
User enters PR URL
       │
       ▼
Angular frontend  ──POST /api/review──▶  ASP.NET Core API
                                               │
                                    Octokit fetches PR diff
                                               │
                                    Anthropic API (streaming)
                                               │
                         ◀──SSE token stream───┘
       │
       ▼
ngx-markdown renders analysis in real time
```

## Key Configuration (appsettings.json)

| Key | Purpose |
|-----|---------|
| `GitHub:PersonalAccessToken` | Read-only PAT for Octokit |
| `Anthropic:ApiKey` | Anthropic API key |
| `Anthropic:Model` | Model ID (default: `claude-sonnet-4-6`) |
| `Session:TimeoutMinutes` | Per-session inactivity timeout |
| `Session:MaxMessagesPerHour` | Rate-limit guard |
