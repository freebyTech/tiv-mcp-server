# tiv-mcp-server

An open source [Model Context Protocol](https://modelcontextprotocol.io) server that lets AI assistants work with a user's [trackItsValue](https://www.trackitsvalue.com) data, with that user's permission.

It is a thin wrapper: it handles sign-in, defines tools, and passes each call through to the trackItsValue API using the user's own token. It holds no business logic and has no database access. Levels and permissions stay enforced by the API.

> **Status:** early. The server runs, requires a valid sign-in token, and exposes one diagnostic tool. The data tools are not built yet, and the trackItsValue API does not accept this server's tokens yet.

Technologies currently used are:

- .NET 10.0
- The official MCP C# SDK (`ModelContextProtocol.AspNetCore`)

## Installing Required Tools

| Tool                             | URL                                            |
| -------------------------------- | ---------------------------------------------- |
| Git                              | https://git-scm.com/downloads                  |
| Install Latest .NET 10.0 SDK     | https://www.microsoft.com/net/download/windows |
| Node 22+ (for the MCP Inspector) | https://nodejs.org/en/                         |

## Configuration

All configuration comes from environment variables. Every setting is required; the server refuses to start if one is missing, and nothing defaults to a real environment. See `.env.example`.

| Variable                | Purpose                                                           |
| ----------------------- | ----------------------------------------------------------------- |
| `MCPAPP__TIVAPIBASEURL` | Base URL of the trackItsValue API that tool calls are passed to   |
| `MCPAPP__STSAUTHORITY`  | OAuth authorization server (Auth0 tenant URL) that issues the tokens this server accepts |
| `MCPAPP__STSAUDIENCE`   | This server's own public URL including `/mcp`. Tokens must be issued for it, and it is published as the OAuth resource |

## Sign-in and authorization

The server is an OAuth 2.0 protected resource, following the MCP authorization specification.

1. An assistant calls `/mcp` without a token and receives `401` with a `WWW-Authenticate` header pointing at the protected resource metadata.
2. The metadata (`/.well-known/oauth-protected-resource/mcp`) names this server as the resource, the authorization server to use, and the scopes it supports.
3. The assistant sends the user to the authorization server to sign in with their existing trackItsValue login and consent.
4. The assistant calls `/mcp` with the access token. The server validates the issuer, the signature (keys discovered from the authority), the lifetime, and that the audience is this server.
5. Tools pass the same token on to the trackItsValue API, which is configured to trust this server's audience, resolves the user, and enforces levels and privileges itself.

Tokens issued for any other audience are rejected.

| Scope        | Meaning                                  |
| ------------ | ---------------------------------------- |
| `data:read`  | The assistant may read the user's data   |
| `data:write` | The assistant may change the user's data |

The scopes are the user's consent for the assistant. The trackItsValue API is where they are enforced.

### Auth0 setup

These steps are done by hand in the Auth0 dashboard; nothing in this repository changes a tenant.

1. **Create an API** for the MCP server. Set its identifier to the server's public URL including `/mcp` (the value of `MCPAPP__STSAUDIENCE`). The identifier cannot be changed later. Use RS256 signing. Each environment (production, beta, local development) needs its own API, because each has its own URL.
2. **Add the permissions** `data:read` and `data:write` to that API.
3. **Enable the Resource Parameter Compatibility Profile** for the tenant (Settings > Advanced), so Auth0 honors the `resource` parameter MCP clients send and issues tokens for the MCP API.
4. **Register a client application** for each assistant or tool that will connect, with no client secret (Single Page or Native type), and add the client's redirect URL to its Allowed Callback URLs. Auth0 compares the URL exactly: `localhost` and `127.0.0.1` are different.
5. **Authorize the application for the API.** On the API's Application Access tab, under User Delegated Access, authorize the application and tick both permissions. Without this Auth0 refuses the sign-in with `Client "..." is not authorized to access resource server "..."`.
6. **Check the access token lifetime** in the API's settings. The default is 24 hours.

Dynamic Client Registration is not covered here yet; the steps above use clients registered by hand.

The trackItsValue API also has to trust the MCP API's identifier as a valid audience. That is configuration of the API, not of Auth0.

## Building this project

```
# Building the server
cd ./src
dotnet build tivMcp.Api.sln

# Running unit tests
dotnet test tivMcp.Api.sln

# Checking code formatting
cd ..
dotnet tool restore
dotnet csharpier --check src

# Build via the docker build file (also runs the unit tests)
docker build ./src -t tiv-mcp-budget
```

## Running locally

Set the variables from `.env.example` in your shell, then:

```
cd ./src/tivMcp.Api
dotnet run
```

The MCP endpoint is `http://localhost:5200/mcp` (Streamable HTTP) and the health probe is `http://localhost:5200/health`.

To explore it with the MCP Inspector:

```
npx @modelcontextprotocol/inspector
```

The current Inspector needs Node 22.23.4 or newer. Open it from the link printed in its terminal, which carries its session token.

Choose the **Streamable HTTP** transport and enter `http://localhost:5200/mcp`. The Inspector will be challenged to sign in, so:

- `MCPAPP__STSAUDIENCE` must be `http://localhost:5200/mcp`, with an API of that identifier at the authorization server.
- In the Inspector's authentication settings, enter the client ID of an application registered as described under "Auth0 setup", and the scope `data:read data:write`.
- The Inspector shows the redirect URL it uses (`http://127.0.0.1:6274/oauth/callback` by default). That exact URL must be in the application's Allowed Callback URLs.

After signing in, run the `get_server_info` tool to confirm the connection.

## Tools

Every tool is read-only. Each one calls a single trackItsValue API endpoint as the signed-in
user and returns the API's JSON unchanged.

| Tool                       | API endpoint                                      | Description                                                                 |
| -------------------------- | ------------------------------------------------- | --------------------------------------------------------------------------- |
| `get_server_info`          | (none)                                            | Name and version of the running MCP server                                  |
| `list_budgets`             | `GET budgeting/budgets/list/{count}`              | The user's most recent monthly budgets, newest first                        |
| `get_budget`               | `GET budgeting/budgets/{name}/{year}/{month}`     | One month's budget: totals, categories, items and actuals                   |
| `list_accounts`            | `GET accounting/accounts/all`                     | The user's accounts                                                         |
| `get_accounts_overview`    | `GET accounting/accounts/all/{year}/{month}`      | All accounts for one month, with balances and that month's transactions     |
| `get_user_status`          | `GET user/status`                                 | The user's level, experience points and counts of budgets and accounts      |
| `get_total_wealth_outlook` | `GET dashboards/total-wealth?range=`              | Wealth over time, one point per closed month                                |
| `export_data`              | `GET export`                                      | A complete copy of the user's data as JSON (the export's `data.json`)       |

What a user may see is decided by the trackItsValue API, not by this server. When the API
refuses a call (not signed in, a feature the user's level has not unlocked, a plan limit, or a
connection that was not granted the needed permission), the tool returns the API's reason as a
plain sentence.

## Repository layout

```
src/
  tivMcp.Api/          The MCP server
  tivMcp.Api.Tests/    Unit tests
  Dockerfile
deploy/
  tiv-mcp-budget/      Helm chart
Jenkinsfile
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE), copyright freebyTech LLC.
