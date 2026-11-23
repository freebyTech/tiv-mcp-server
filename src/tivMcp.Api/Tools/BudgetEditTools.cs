using System.ComponentModel;
using ModelContextProtocol.Server;
using tivMcp.Api.Services.Interfaces;

namespace tivMcp.Api.Tools;

/// <summary>
/// Tools that change one month's budget. Each mirrors a tool of the trackItsValue agent API, with
/// the same name and arguments, plus the year, month and budget name that say which budget to
/// work on. A tool checks its arguments and hands the rest to <see cref="IBudgetEditor"/>: the
/// agent API applies the change and the trackItsValue API saves it and decides whether it is
/// allowed.
/// </summary>
[McpServerToolType]
public sealed class BudgetEditTools
{
  private const string NamesHint = " Use the exact category and item names shown by get_budget_summary.";
  private const string YearHint = "The four-digit year of the budget, for example 2026.";
  private const string MonthHint = "The month of the budget, 1 (January) to 12 (December).";
  private const string BudgetNameHint = "The budget's name. Leave empty for the user's main budget, which is named 'default'.";
  private const string CategoryToNarrowHint = "The category the item is in. Only needed when two categories have an item with the same name.";

  private readonly IBudgetEditor _budgetEditor;

  /// <summary>
  /// Constructor.
  /// </summary>
  public BudgetEditTools(IBudgetEditor budgetEditor)
  {
    _budgetEditor = budgetEditor;
  }

  /// <summary>
  /// Agent API tool update_estimates.
  /// </summary>
  [McpServerTool(Name = "update_estimates", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Set the budget's estimated total income and estimated total spending for the month. Both values replace the current ones.")]
  public Task<string> UpdateEstimates(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The new estimated total income for the month.")] decimal estimatedIncome,
    [Description("The new estimated total spending for the month.")] decimal estimatedSpending,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?> { ["estimated_income"] = estimatedIncome, ["estimated_spending"] = estimatedSpending };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "update_estimates", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool add_category.
  /// </summary>
  [McpServerTool(Name = "add_category", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description("Add a new category to the budget. Every category is based on a category template; call get_budget_summary with includeTemplates set to true to see the template names available.")]
  public Task<string> AddCategory(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the new category, for example 'Transportation'.")] string description,
    [Description("The exact name of the category template to base it on, for example 'Food & Essentials'.")] string categoryTemplateDescription,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["description"] = ToolArguments.RequireText(nameof(description), description),
      ["category_template_description"] = ToolArguments.RequireText(nameof(categoryTemplateDescription), categoryTemplateDescription),
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "add_category", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool update_category.
  /// </summary>
  [McpServerTool(Name = "update_category", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Rename a category in the budget." + NamesHint)]
  public Task<string> UpdateCategory(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The category's current name.")] string categoryDescription,
    [Description("The category's new name.")] string newDescription,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["category_description"] = ToolArguments.RequireText(nameof(categoryDescription), categoryDescription),
      ["new_description"] = ToolArguments.RequireText(nameof(newDescription), newDescription),
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "update_category", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool delete_category.
  /// </summary>
  [McpServerTool(Name = "delete_category", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description("Delete a category from the budget, together with every item in it and every transaction under those items. This cannot be undone; confirm with the user first." + NamesHint)]
  public Task<string> DeleteCategory(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the category to delete.")] string categoryDescription,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?> { ["category_description"] = ToolArguments.RequireText(nameof(categoryDescription), categoryDescription) };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "delete_category", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool add_item.
  /// </summary>
  [McpServerTool(Name = "add_item", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Add a new budget item to a category. Every item is based on an item template allowed for that category; call get_budget_summary with includeTemplates set to true to see the template names available."
      + NamesHint
  )]
  public Task<string> AddItem(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the category to add the item to.")] string categoryDescription,
    [Description("The name of the new item, for example 'Groceries'.")] string itemDescription,
    [Description("The exact name of the item template to base it on, for example 'Groceries' or 'Paycheck(s)'.")] string itemTemplateDescription,
    [Description("The amount to budget for the item. Defaults to 0.")] decimal amountBudgeted = 0,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["category_description"] = ToolArguments.RequireText(nameof(categoryDescription), categoryDescription),
      ["item_description"] = ToolArguments.RequireText(nameof(itemDescription), itemDescription),
      ["item_template_description"] = ToolArguments.RequireText(nameof(itemTemplateDescription), itemTemplateDescription),
      ["amount_budgeted"] = amountBudgeted,
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "add_item", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool update_item.
  /// </summary>
  [McpServerTool(Name = "update_item", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Change a budget item's budgeted amount, its name, or both." + NamesHint)]
  public Task<string> UpdateItem(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The item's current name, for example 'Groceries'.")] string itemDescription,
    [Description("The new budgeted amount. Leave empty to keep the current amount.")] decimal? newAmountBudgeted = null,
    [Description("The item's new name. Leave empty to keep the current name.")] string? newDescription = null,
    [Description(CategoryToNarrowHint)] string? categoryDescription = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["item_description"] = ToolArguments.RequireText(nameof(itemDescription), itemDescription),
      ["new_amount_budgeted"] = newAmountBudgeted,
      ["new_description"] = ToolArguments.OptionalText(nameof(newDescription), newDescription),
      ["category_description"] = ToolArguments.OptionalText(nameof(categoryDescription), categoryDescription),
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "update_item", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool delete_item.
  /// </summary>
  [McpServerTool(Name = "delete_item", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description("Delete a budget item together with every transaction under it. This cannot be undone; confirm with the user first." + NamesHint)]
  public Task<string> DeleteItem(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the item to delete.")] string itemDescription,
    [Description(CategoryToNarrowHint)] string? categoryDescription = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["item_description"] = ToolArguments.RequireText(nameof(itemDescription), itemDescription),
      ["category_description"] = ToolArguments.OptionalText(nameof(categoryDescription), categoryDescription),
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "delete_item", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool add_actual.
  /// </summary>
  [McpServerTool(Name = "add_actual", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Record a transaction (an actual) against a budget item, such as a purchase or a paycheck. "
      + "For a purchase paid by credit card set isCreditWithdrawl to true. "
      + "If the item has a default account (shown as [default-acct:...] in get_budget_summary) no account ids are needed unless the user names a different card. "
      + "If the item has no default account, a credit card purchase needs accountLinkId and accountCategoryLinkId; call get_available_accounts to find them."
      + NamesHint
  )]
  public Task<string> AddActual(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the budget item, for example 'Groceries'.")] string itemDescription,
    [Description("The transaction amount, as a positive number.")] decimal amount,
    [Description("What the transaction was, for example the shop's name.")] string description,
    [Description("The transaction date written as YYYY-MM-DD. It should fall in the budget's month.")] string date,
    [Description(CategoryToNarrowHint)] string? categoryDescription = null,
    [Description("True when the purchase was paid with a credit account.")] bool isCreditWithdrawl = false,
    [Description("The full 36-character id of the account to link the transaction to. See the tool description for when it is needed.")] string? accountLinkId = null,
    [Description("The full 36-character id of the account category to link the transaction to. Needed whenever accountLinkId is given.")] string? accountCategoryLinkId = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["item_description"] = ToolArguments.RequireText(nameof(itemDescription), itemDescription),
      ["amount"] = amount,
      ["description"] = ToolArguments.RequireText(nameof(description), description),
      ["date"] = ToolArguments.RequireDate(nameof(date), date),
      ["category_description"] = ToolArguments.OptionalText(nameof(categoryDescription), categoryDescription),
      ["is_credit_withdrawl"] = isCreditWithdrawl,
      ["account_link_id"] = ToolArguments.OptionalText(nameof(accountLinkId), accountLinkId),
      ["account_category_link_id"] = ToolArguments.OptionalText(nameof(accountCategoryLinkId), accountCategoryLinkId),
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "add_actual", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool update_actual.
  /// </summary>
  [McpServerTool(Name = "update_actual", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description(
    "Change a transaction's amount, description or date. The transaction is found by its current description under the item; "
      + "when several transactions under the item share that description, give actualDate or actualAmount to say which one."
      + NamesHint
  )]
  public Task<string> UpdateActual(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the budget item the transaction is under.")] string itemDescription,
    [Description("The transaction's current description.")] string actualDescription,
    [Description("The new amount. Leave empty to keep the current amount.")] decimal? newAmount = null,
    [Description("The new description. Leave empty to keep the current description.")] string? newDescription = null,
    [Description("The new date written as YYYY-MM-DD. Leave empty to keep the current date.")] string? newDate = null,
    [Description(CategoryToNarrowHint)] string? categoryDescription = null,
    [Description("The transaction's current date written as YYYY-MM-DD, to tell apart transactions with the same description.")] string? actualDate = null,
    [Description("The transaction's current amount, to tell apart transactions with the same description.")] decimal? actualAmount = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["item_description"] = ToolArguments.RequireText(nameof(itemDescription), itemDescription),
      ["actual_description"] = ToolArguments.RequireText(nameof(actualDescription), actualDescription),
      ["new_amount"] = newAmount,
      ["new_description"] = ToolArguments.OptionalText(nameof(newDescription), newDescription),
      ["new_date"] = ToolArguments.OptionalDate(nameof(newDate), newDate),
      ["category_description"] = ToolArguments.OptionalText(nameof(categoryDescription), categoryDescription),
      ["actual_date"] = ToolArguments.OptionalDate(nameof(actualDate), actualDate),
      ["actual_amount"] = actualAmount,
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "update_actual", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool delete_actual.
  /// </summary>
  [McpServerTool(Name = "delete_actual", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Delete a transaction from a budget item. This cannot be undone; confirm with the user first. "
      + "When several transactions under the item share the description, give actualDate or actualAmount to say which one."
      + NamesHint
  )]
  public Task<string> DeleteActual(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the budget item the transaction is under.")] string itemDescription,
    [Description("The description of the transaction to delete.")] string actualDescription,
    [Description(CategoryToNarrowHint)] string? categoryDescription = null,
    [Description("The transaction's date written as YYYY-MM-DD, to tell apart transactions with the same description.")] string? actualDate = null,
    [Description("The transaction's amount, to tell apart transactions with the same description.")] decimal? actualAmount = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["item_description"] = ToolArguments.RequireText(nameof(itemDescription), itemDescription),
      ["actual_description"] = ToolArguments.RequireText(nameof(actualDescription), actualDescription),
      ["category_description"] = ToolArguments.OptionalText(nameof(categoryDescription), categoryDescription),
      ["actual_date"] = ToolArguments.OptionalDate(nameof(actualDate), actualDate),
      ["actual_amount"] = actualAmount,
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "delete_actual", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool move_budget.
  /// </summary>
  [McpServerTool(Name = "move_budget", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description("Move part of one item's budgeted amount to another item. Only the budgeted amounts change; no transactions are moved." + NamesHint)]
  public Task<string> MoveBudget(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the item to take the amount from.")] string fromItem,
    [Description("The name of the item to give the amount to.")] string toItem,
    [Description("The amount to move, as a positive number.")] decimal amount,
    [Description("The category of the item to take from. Only needed when two categories have an item with that name.")] string? fromCategory = null,
    [Description("The category of the item to give to. Only needed when two categories have an item with that name.")] string? toCategory = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["from_item"] = ToolArguments.RequireText(nameof(fromItem), fromItem),
      ["to_item"] = ToolArguments.RequireText(nameof(toItem), toItem),
      ["amount"] = amount,
      ["from_category"] = ToolArguments.OptionalText(nameof(fromCategory), fromCategory),
      ["to_category"] = ToolArguments.OptionalText(nameof(toCategory), toCategory),
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "move_budget", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool rectify_budget.
  /// </summary>
  [McpServerTool(Name = "rectify_budget", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description(
    "Rectify the whole budget: set every item's budgeted amount to what was actually spent on it. "
      + "Items with no spending are set to zero and overspent items are raised to match. This overwrites every budgeted amount in the month; confirm with the user first."
  )]
  public Task<string> RectifyBudget(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "rectify_budget", new Dictionary<string, object?>(), cancellationToken);
  }

  /// <summary>
  /// Agent API tool rectify_category.
  /// </summary>
  [McpServerTool(Name = "rectify_category", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Rectify one category: set the budgeted amount of every item in it to what was actually spent on that item." + NamesHint)]
  public Task<string> RectifyCategory(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the category to rectify.")] string categoryDescription,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?> { ["category_description"] = ToolArguments.RequireText(nameof(categoryDescription), categoryDescription) };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "rectify_category", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool rectify_item.
  /// </summary>
  [McpServerTool(Name = "rectify_item", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description("Rectify one item: set its budgeted amount to what was actually spent on it." + NamesHint)]
  public Task<string> RectifyItem(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the item to rectify.")] string itemDescription,
    [Description(CategoryToNarrowHint)] string? categoryDescription = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?>
    {
      ["item_description"] = ToolArguments.RequireText(nameof(itemDescription), itemDescription),
      ["category_description"] = ToolArguments.OptionalText(nameof(categoryDescription), categoryDescription),
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "rectify_item", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool get_available_accounts.
  /// </summary>
  [McpServerTool(Name = "get_available_accounts", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
  [Description(
    "List the accounts a budget transaction can be linked to, each with its id and its categories' ids. "
      + "Use the account's id as accountLinkId and a category's id as accountCategoryLinkId when calling add_actual. "
      + "This is for preparing a change, so the connection needs permission to make changes."
  )]
  public Task<string> GetAvailableAccounts(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    return _budgetEditor.QueryAsync(year, month, budgetName, "get_available_accounts", cancellationToken);
  }
}
