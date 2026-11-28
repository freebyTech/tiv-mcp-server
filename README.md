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
| `MCPAPP__TIVAGENTAPIBASEURL` | Base URL of the trackItsValue agent API, which writes the compact summaries |
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

Every tool runs as the signed-in user. This server is a pass-through to two services that
each keep their own job: the trackItsValue API owns the data and decides what a user may do,
and the trackItsValue agent API owns the conversion between the API's JSON and the compact form
an assistant works with. No budget rules live here.

### Reading

Most read tools call a single trackItsValue API endpoint and return its JSON unchanged. The two
summary tools get the same JSON, hand it unchanged to the agent API, and return the short text
summary it writes.

| Tool                       | API endpoint                                      | Description                                                                 |
| -------------------------- | ------------------------------------------------- | --------------------------------------------------------------------------- |
| `get_server_info`          | (none)                                            | Name and version of the running MCP server                                  |
| `list_budgets`             | `GET budgeting/budgets/list/{count}`              | The user's most recent monthly budgets, newest first                        |
| `get_budget`               | `GET budgeting/budgets/{name}/{year}/{month}`     | One month's budget: totals, categories, items and actuals                   |
| `get_budget_summary`       | the same, then agent `POST budget-agent/summarize-budget` | One month's budget as a short text summary; far smaller than `get_budget`. Optional `category` and `item` narrow it to one part |
| `list_accounts`            | `GET accounting/accounts/all`                     | The user's accounts                                                         |
| `get_accounts_overview`    | `GET accounting/accounts/all/{year}/{month}`      | All accounts for one month, with balances and that month's transactions     |
| `get_accounts_summary`     | the same, then agent `POST budget-agent/summarize-accounts` | All accounts for one month as a short text summary; far smaller than `get_accounts_overview` |
| `get_user_status`          | `GET user/status`                                 | The user's level, experience points and counts of budgets and accounts      |
| `list_trend_items`         | `GET budgeting/lineage/groups`                    | The budget items that can be followed across months, each with its id. Needs level 4 |
| `get_item_trends`          | `GET budgeting/lineage/trends?continuanceIds=`    | Budgeted and actual amounts per month for the given items. Needs level 4    |
| `get_total_wealth_outlook` | `GET dashboards/total-wealth?range=`              | Wealth over time, one point per closed month                                |

### Changing a budget

Each of these mirrors a tool of the agent API, with the same name and arguments, plus the year,
month and budget name that say which budget to work on. The server gets the budget, the month's
accounts and the budget templates from the API, has the agent API apply the tool
(`POST budget-agent/apply-tool`), and saves the edited budget with `PUT budgeting/budgets`, the
same call the website makes on Save. If the tool cannot do what was asked, or either service
refuses, nothing is saved. These tools need a connection that was granted `data:write`.

| Tool                     | Description                                                                |
| ------------------------ | -------------------------------------------------------------------------- |
| `create_budget`          | Create a month's budget as a copy of an earlier month, without its transactions (`POST budgeting/budgets/copy`) |
| `add_actual`             | Record a transaction against a budget item                                 |
| `add_actuals`            | Record up to 50 transactions in one month at once, all or nothing, saved once; optionally as one combined transaction |
| `split_actual`           | Split a transaction between budget items; the parts stay one combined transaction |
| `combine_actuals`        | Mark transactions as one combined transaction (one bank line), or add to an existing combination |
| `separate_actuals`       | Take transactions out of their combined transaction                        |
| `update_actual`          | Change a transaction's amount, description or date                         |
| `move_actual`            | Move a transaction to another budget item                                  |
| `set_actual_account`     | Change which account a card purchase, cash withdrawal or transfer is linked to, or remove the link |
| `set_item_account`       | Set or remove a budget item's default account                              |
| `reorder_category`       | Move a category to another place in the budget                             |
| `reorder_item`           | Move an item to another place within its category                          |
| `set_budget_prediction`  | Snapshot what each category is budgeted, to measure the month against (`POST budgeting/budgets/{id}/set-prediction`) |
| `close_budget`           | Close a month's budget (`POST budgeting/budgets/{id}/close`)             |
| `reopen_budget`          | Reopen a closed budget (`POST budgeting/budgets/{id}/reopen`)            |
| `delete_budget`          | Delete a month's whole budget (`DELETE budgeting/budgets`)                |
| `delete_actual`          | Delete a transaction                                                       |
| `add_item`               | Add a budget item to a category                                            |
| `update_item`            | Change an item's budgeted amount or name                                   |
| `delete_item`            | Delete an item and its transactions                                        |
| `add_category`           | Add a category                                                             |
| `update_category`        | Rename a category                                                          |
| `delete_category`        | Delete a category, its items and their transactions                        |
| `move_budget`            | Move budgeted amount from one item to another                              |
| `update_estimates`       | Set the month's estimated income and spending                              |
| `rectify_item`           | Set an item's budgeted amount to what was spent                            |
| `rectify_category`       | The same for every item in a category                                      |
| `rectify_budget`         | The same for every item in the budget                                      |
| `get_available_accounts` | List the accounts and account categories a transaction can be linked to (changes nothing) |

### Changing accounts

This tool makes one call to the trackItsValue API and returns its JSON unchanged. It needs a
connection that was granted `data:write`.

| Tool                          | API endpoint                                    | Description                                                                 |
| ----------------------------- | ----------------------------------------------- | --------------------------------------------------------------------------- |
| `verify_account_transactions` | `POST accounting/accounts/verify-transactions`  | Mark one account's transactions in one month as verified (or not), for the whole month or the 1st through a given day. Has a dry run, and is safe to repeat |

The rest of the account tools do what the website's account page does. Most mirror a tool of the
agent API: the server gets the month's accounts, the account templates and the account types from
the API, has the agent API apply the tool (`POST budget-agent/apply-account-tools`), and saves the
edited accounts with `PUT accounting/accounts`, the call the website makes on Save. If the tool
cannot do what was asked, or either service refuses, nothing is saved. Accounts, categories and
transactions are given by name.

| Tool                               | Description                                                              |
| ---------------------------------- | ------------------------------------------------------------------------ |
| `list_account_options`             | List the account types, their sub types, and each sub type's category and transaction types (read-only) |
| `create_account`                   | Create an account (`POST accounting/accounts/createAccount`)            |
| `rename_account`                   | Rename an account                                                        |
| `remove_account`                   | Hide an account that has history, or delete one that never had a transaction |
| `add_account_category`             | Add a category (a fund) to an account                                    |
| `rename_account_category`          | Rename a category                                                        |
| `add_account_transaction`          | Add a deposit or withdrawal directly to an account                       |
| `update_account_transaction`       | Change a transaction, or move it to another category of the account     |
| `delete_account_transaction`       | Delete a transaction that was entered directly in an account            |
| `set_account_transaction_verified` | Mark one transaction verified, or clear the mark                         |
| `combine_account_transactions`     | Mark transactions of one account as one combined transaction, or extend a combination |
| `separate_account_transactions`    | Take transactions out of their combined transaction                      |
| `transfer_between_accounts`        | Move money directly between two accounts, outside the budget             |
| `set_account_balance`              | Preview or set an account's balance on given dates (`POST accounting/accounts/{id}/set-balance-as-of`) |
| `get_months_status`                | Which months' budgets and accounts are closed, can be closed, or are blocked (`GET accounting/accounts/months-status`, read-only) |
| `close_account_month`              | Close a month for all accounts (`POST accounting/accounts/close-month/{year}/{month}`) |
| `reopen_account_month`             | Reopen a closed month (`POST accounting/accounts/reopen-month/{year}/{month}`) |

Not covered: recurring account transactions, editing an existing Set Value entry, image
attachments, and everything about holdings.

What a user may see is decided by the trackItsValue API, not by this server. When the API
refuses a call (not signed in, a feature the user's level has not unlocked, a plan limit, or a
connection that was not granted the needed permission), the tool returns the API's reason as a
plain sentence.

## Monthly tool call allowance

Tool calls count toward a monthly allowance that depends on the user's plan. The trackItsValue
API holds the count and the limits (its `mcp-tool` usage bucket); this server knows neither.
Before a tool runs the server asks the API whether another call is allowed
(`GET usage/check?bucket=mcp-tool`), and after a tool succeeds it reports the call
(`POST usage/record`), both as the signed-in user.

- When the allowance is used up, the tool does not run and the error gives the limit, the plan
  and the date the count starts again.
- A call that is refused, fails, or has a bad argument is not counted.
- `get_server_info` is never counted.
- If the check or the report itself fails, the tool call is not affected; the failure is logged.

## When a tool call fails

Every tool call gets a short reference, eight hex characters. The server:

- sends it to both trackItsValue services with every request, in the `X-Request-Id` header;
- writes a warning to its own log for each failed request: the reference, the service, the method and path, the status, how long it took, and the start of the response body;
- shows it in the error when the failure does not explain itself, for example `Reference 3f9a1c2e (budget API).`

To find out why a call failed, search this server's log for the reference. The response body is in the log only: a failing service can put internal detail in it, so it is never shown to the assistant.

A tool that changes a budget also says which step failed and what that means for the data:

| Step                    | The error starts with            | What it means                                                                                       |
| ----------------------- | -------------------------------- | --------------------------------------------------------------------------------------------------- |
| Reading from the API    | `Reading the ... failed.`        | Nothing was saved                                                                                   |
| Editing in the agent API | `Working out the change failed.` | Nothing was saved                                                                                   |
| Saving through the API  | `Saving the budget failed.`      | Refused (a 4xx): nothing was saved. A fault or no answer: unknown, so read the budget before retrying |

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
