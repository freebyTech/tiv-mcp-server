namespace tivMcp.Api.Constants;

/// <summary>
/// What an assistant is told about this server when it connects, before it has called any tool:
/// the things that hold for every tool and so do not belong in one tool's description.
/// </summary>
public static class ServerInstructions
{
  /// <summary>
  /// The instructions sent in the answer to the assistant's initialize request.
  /// </summary>
  public const string Text =
    "This server reads and changes the signed-in user's trackItsValue budgets and accounts. "
    + "Reading: prefer get_budget_summary and get_accounts_summary, which are far smaller than get_budget and get_accounts_overview. "
    + "Changing a budget: a tool that succeeds answers with 'Saved.' and what changed. "
    + "To record several transactions in one month use add_actuals, not add_actual repeatedly. "
    + "When one payment covers several budget items, record it with add_actuals and combined set to true, or divide a recorded transaction with split_actual, so it still matches one line on the statement. "
    + "If a month has no budget yet, create_budget makes one. "
    + "To mark an account's transactions as checked against a statement use verify_account_transactions, one month per call; it changes only that account and is safe to repeat. "
    + "Accounts: income and spending are recorded in the budget, which posts them to the accounts by itself; use the account tools only for what is not in the budget, such as interest (add_account_transaction), moving money between accounts (transfer_between_accounts) or setting a balance (set_account_balance). In get_accounts_summary a transaction marked linked came from the budget and is changed there. "
    + "When a tool fails: the error says which step failed and whether anything was saved. "
    + "'Nothing was saved' means the budget is unchanged, so fixing the request or trying again cannot record anything twice. "
    + "'The save may or may not have gone through' means the outcome is unknown: read the budget with get_budget_summary before retrying, so nothing is recorded twice. "
    + "An error that gives 'Reference' and an eight-character id is a fault on the trackItsValue side, not a mistake in the request; tell the user that reference exactly, because it finds the failure in the service's log. "
    + "Tool calls count toward a monthly allowance set by the user's plan; when it is used up every tool refuses until the date the error gives. "
    + "A refusal that names a level, a plan or a permission is the user's account or this connection's access, and retrying will not change it.";
}
