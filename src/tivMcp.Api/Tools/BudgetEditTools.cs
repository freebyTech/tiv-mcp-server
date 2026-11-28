using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using tivMcp.Api.Models;
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
  // On every tool that saves: what to do when the save is in doubt.
  private const string SaveHint = " If this fails with 'The save may or may not have gone through', check with get_budget_summary before retrying, so nothing is recorded twice.";
  private const string NamesHint = " Use the exact category and item names shown by get_budget_summary." + SaveHint;
  private const string YearHint = "The four-digit year of the budget, for example 2026.";
  private const string MonthHint = "The month of the budget, 1 (January) to 12 (December).";
  private const string BudgetNameHint = "The budget's name. Leave empty for the user's main budget, which is named 'default'.";
  private const string CategoryToNarrowHint = "The category the item is in. Only needed when two categories have an item with the same name.";

  /// <summary>The most transactions add_actuals takes in one call.</summary>
  public const int MostActualsAtOnce = 50;

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
  [Description("Set the budget's estimated total income and estimated total spending for the month. Both values replace the current ones." + SaveHint)]
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
  [Description(
    "Add a new category to the budget. Every category is based on a category template; call get_budget_summary with includeTemplates set to true to see the template names available." + SaveHint
  )]
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
  /// When to pass account ids on a new transaction. An account id is a link, and the API posts a
  /// link as the far side of a transfer, so ids that only repeat a default are never wanted.
  /// </summary>
  internal const string AccountIdsHint =
    "Leave accountLinkId and accountCategoryLinkId empty when the transaction uses the item's default account "
    + "(shown as [default-acct:...] in get_budget_summary). Only pass account ids to use a different account than the default. "
    + "Every transaction that is not a credit card purchase is posted to the budget's own account automatically, so never pass that account's ids either; "
    + "on such a transaction account ids mean a transfer to or from the account named. ";

  /// <summary>
  /// Agent API tool add_actual.
  /// </summary>
  [McpServerTool(Name = "add_actual", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Record a transaction (an actual) against a budget item, such as a purchase or a paycheck. "
      + "To record several in the same month use add_actuals instead: one call, one save. "
      + "For a purchase paid by credit card set isCreditWithdrawl to true. "
      + AccountIdsHint
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
    [Description(NewActual.AccountLinkIdHint)] string? accountLinkId = null,
    [Description(NewActual.AccountCategoryLinkIdHint)] string? accountCategoryLinkId = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var actual = new NewActual
    {
      ItemDescription = itemDescription,
      Amount = amount,
      Description = description,
      Date = date,
      CategoryDescription = categoryDescription,
      IsCreditWithdrawl = isCreditWithdrawl,
      AccountLinkId = accountLinkId,
      AccountCategoryLinkId = accountCategoryLinkId,
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "add_actual", AddActualArguments(actual, string.Empty), cancellationToken);
  }

  /// <summary>
  /// Agent API tool add_actual, once for each transaction, applied to the budget together.
  /// </summary>
  [McpServerTool(Name = "add_actuals", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
  [Description(
    "Record several transactions (actuals) in one month's budget at once, for example a pile of receipts or a statement's charges. "
      + "Prefer this over calling add_actual repeatedly: the budget is saved once. "
      + "Each entry has the same fields and rules as add_actual. "
      + AccountIdsHint
      + "All or nothing: if any entry cannot be recorded, none are, and the error says which entry and why. "
      + "All entries go into the one budget month given; transactions of another month need their own call. "
      + "Set combined to true when the entries were ONE payment on the bank or card statement, for example a $120 shop visit that was $90 of groceries and $30 of household goods: "
      + "they are recorded as one combined transaction. Combined entries need the same date and the same payment method, and at least 2 of them."
      + NamesHint
  )]
  public Task<string> AddActuals(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The transactions to record, at least 1 and at most 50.")] NewActual[] actuals,
    [Description(BudgetNameHint)] string? budgetName = null,
    [Description("True when the entries were one payment on the bank or card statement and should be recorded as one combined transaction. Defaults to false.")] bool combined = false,
    CancellationToken cancellationToken = default
  )
  {
    if (actuals == null || actuals.Length == 0)
    {
      throw new McpException($"{nameof(actuals)} must list at least one transaction.");
    }

    if (actuals.Length > MostActualsAtOnce)
    {
      throw new McpException($"{nameof(actuals)} can list at most {MostActualsAtOnce} transactions, but {actuals.Length} were given. Send them in several calls.");
    }

    if (combined && actuals.Length < 2)
    {
      throw new McpException($"{nameof(actuals)} must list at least 2 transactions when {nameof(combined)} is true. One transaction is not a combination.");
    }

    var calls = new List<ToolCall>(actuals.Length);
    for (var index = 0; index < actuals.Length; index++)
    {
      // Entries are numbered from 1 in messages, as a person would count them.
      var entry = $"{nameof(actuals)} entry {index + 1}";
      if (actuals[index] == null)
      {
        throw new McpException($"{entry} is empty.");
      }

      calls.Add(new ToolCall("add_actual", AddActualArguments(actuals[index], entry + ": ")));
    }

    if (combined)
    {
      // One agent API tool takes the whole list, so it can check the entries against each other
      // and give them one combination. Each entry is add_actual's arguments, as above.
      var entries = calls.Select(call => WithoutNulls(new Dictionary<string, object?>(call.Arguments))).ToList();
      var arguments = new Dictionary<string, object?> { ["actuals"] = entries };

      return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "add_combined_actuals", arguments, cancellationToken);
    }

    return _budgetEditor.ApplyAllAndSaveAsync(year, month, budgetName, calls, cancellationToken);
  }

  /// <summary>
  /// Checks one transaction and returns it as the agent API's add_actual arguments.
  /// </summary>
  /// <param name="actual">The transaction.</param>
  /// <param name="entry">Text put in front of an argument's name in an error, to say which of several transactions is wrong.</param>
  private static Dictionary<string, object?> AddActualArguments(NewActual actual, string entry)
  {
    return new Dictionary<string, object?>
    {
      ["item_description"] = ToolArguments.RequireText(entry + "itemDescription", actual.ItemDescription),
      ["amount"] = actual.Amount,
      ["description"] = ToolArguments.RequireText(entry + "description", actual.Description),
      ["date"] = ToolArguments.RequireDate(entry + "date", actual.Date),
      ["category_description"] = ToolArguments.OptionalText(entry + "categoryDescription", actual.CategoryDescription),
      ["is_credit_withdrawl"] = actual.IsCreditWithdrawl,
      ["account_link_id"] = ToolArguments.OptionalText(entry + "accountLinkId", actual.AccountLinkId),
      ["account_category_link_id"] = ToolArguments.OptionalText(entry + "accountCategoryLinkId", actual.AccountCategoryLinkId),
    };
  }

  /// <summary>
  /// What a combined transaction is, said once on every tool that makes or changes one.
  /// </summary>
  internal const string CombinedHint =
    "A combined transaction is several budget transactions that are ONE line on the bank or card statement; " + "get_budget_summary marks its members with the same [combined:...] tag. ";

  /// <summary>The most split lines split_actual takes in one call.</summary>
  public const int MostSplitLinesAtOnce = 20;

  /// <summary>
  /// Agent API tool split_actual.
  /// </summary>
  [McpServerTool(Name = "split_actual", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Split a transaction between budget items, for example when medicine was bought with the groceries. "
      + "Each split line becomes a new transaction on its item with the original's date and payment method "
      + "(a credit card purchase splits into purchases on the same card), and the original keeps what the lines do not claim, so the total paid never changes. "
      + "The parts stay one combined transaction. "
      + CombinedHint
      + "A line goes to a different item that is already in this budget and on the same side (expense or income) as the original; "
      + "the lines must add up to less than the original amount. "
      + "When several transactions under the item share the description, give actualDate or actualAmount to say which one."
      + NamesHint
  )]
  public Task<string> SplitActual(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The name of the budget item the transaction is under.")] string itemDescription,
    [Description("The description of the transaction to split.")] string actualDescription,
    [Description("The parts to move to other items, at least 1 and at most 20.")] SplitLine[] splits,
    [Description(CategoryToNarrowHint)] string? categoryDescription = null,
    [Description("The transaction's date written as YYYY-MM-DD, to tell apart transactions with the same description.")] string? actualDate = null,
    [Description("The transaction's amount, to tell apart transactions with the same description.")] decimal? actualAmount = null,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    if (splits == null || splits.Length == 0)
    {
      throw new McpException($"{nameof(splits)} must list at least one part to move to another item.");
    }

    if (splits.Length > MostSplitLinesAtOnce)
    {
      throw new McpException($"{nameof(splits)} can list at most {MostSplitLinesAtOnce} parts, but {splits.Length} were given.");
    }

    var lines = new List<Dictionary<string, object?>>(splits.Length);
    for (var index = 0; index < splits.Length; index++)
    {
      var entry = $"{nameof(splits)} entry {index + 1}";
      var line = splits[index] ?? throw new McpException($"{entry} is empty.");
      lines.Add(
        WithoutNulls(
          new Dictionary<string, object?>
          {
            ["item_description"] = ToolArguments.RequireText($"{entry}: itemDescription", line.ItemDescription),
            ["amount"] = line.Amount,
            ["description"] = ToolArguments.OptionalText($"{entry}: description", line.Description),
            ["category_description"] = ToolArguments.OptionalText($"{entry}: categoryDescription", line.CategoryDescription),
          }
        )
      );
    }

    var arguments = new Dictionary<string, object?>
    {
      ["item_description"] = ToolArguments.RequireText(nameof(itemDescription), itemDescription),
      ["actual_description"] = ToolArguments.RequireText(nameof(actualDescription), actualDescription),
      ["splits"] = lines,
      ["category_description"] = ToolArguments.OptionalText(nameof(categoryDescription), categoryDescription),
      ["actual_date"] = ToolArguments.OptionalDate(nameof(actualDate), actualDate),
      ["actual_amount"] = actualAmount,
    };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "split_actual", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool combine_actuals.
  /// </summary>
  [McpServerTool(Name = "combine_actuals", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Mark transactions already in the budget as one combined transaction, for example one transfer that funded several savings goals, "
      + "one cash withdrawal for several envelopes, or one check or card charge covering several items. "
      + CombinedHint
      + "If one of the transactions is already combined, the others are added to that combination. "
      + "The transactions must have the same date, be on the same account and be paid the same way (all by credit card, all cash, or neither); "
      + "transfers to another account combine only with other transfers to that same account. "
      + "No amount, date or description changes. To record new transactions already combined use add_actuals with combined set to true; "
      + "to divide one transaction between items use split_actual."
      + NamesHint
  )]
  public Task<string> CombineActuals(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The transactions to combine, at least 2 and at most 50.")] ActualReference[] actuals,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?> { ["actuals"] = ActualReferenceArguments(actuals, 2) };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "combine_actuals", arguments, cancellationToken);
  }

  /// <summary>
  /// Agent API tool separate_actuals.
  /// </summary>
  [McpServerTool(Name = "separate_actuals", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
  [Description(
    "Take transactions out of the combined transaction they belong to. The transactions themselves stay, with their amounts, and only stop being marked as one bank line. "
      + CombinedHint
      + "Other members stay combined with each other; to dissolve a combination, name every one of its transactions."
      + NamesHint
  )]
  public Task<string> SeparateActuals(
    [Description(YearHint)] int year,
    [Description(MonthHint)] int month,
    [Description("The transactions to take out of their combined transaction, at least 1 and at most 50.")] ActualReference[] actuals,
    [Description(BudgetNameHint)] string? budgetName = null,
    CancellationToken cancellationToken = default
  )
  {
    var arguments = new Dictionary<string, object?> { ["actuals"] = ActualReferenceArguments(actuals, 1) };

    return _budgetEditor.ApplyAndSaveAsync(year, month, budgetName, "separate_actuals", arguments, cancellationToken);
  }

  /// <summary>
  /// Checks a list of named transactions and returns it as the agent API's arguments.
  /// </summary>
  /// <param name="actuals">The transactions named.</param>
  /// <param name="fewest">The fewest the tool can work with.</param>
  private static List<Dictionary<string, object?>> ActualReferenceArguments(ActualReference[] actuals, int fewest)
  {
    if (actuals == null || actuals.Length < fewest)
    {
      throw new McpException($"{nameof(actuals)} must list at least {fewest} transaction{(fewest == 1 ? string.Empty : "s")}.");
    }

    if (actuals.Length > MostActualsAtOnce)
    {
      throw new McpException($"{nameof(actuals)} can list at most {MostActualsAtOnce} transactions, but {actuals.Length} were given.");
    }

    var references = new List<Dictionary<string, object?>>(actuals.Length);
    for (var index = 0; index < actuals.Length; index++)
    {
      var entry = $"{nameof(actuals)} entry {index + 1}";
      var actual = actuals[index] ?? throw new McpException($"{entry} is empty.");
      references.Add(
        WithoutNulls(
          new Dictionary<string, object?>
          {
            ["item_description"] = ToolArguments.RequireText($"{entry}: itemDescription", actual.ItemDescription),
            ["actual_description"] = ToolArguments.RequireText($"{entry}: actualDescription", actual.ActualDescription),
            ["category_description"] = ToolArguments.OptionalText($"{entry}: categoryDescription", actual.CategoryDescription),
            ["actual_date"] = ToolArguments.OptionalDate($"{entry}: actualDate", actual.ActualDate),
            ["actual_amount"] = actual.ActualAmount,
          }
        )
      );
    }

    return references;
  }

  /// <summary>
  /// Drops what the caller left out of an entry inside a list. The editor does this for a tool's
  /// own arguments, but not for the entries of a list among them.
  /// </summary>
  private static Dictionary<string, object?> WithoutNulls(Dictionary<string, object?> arguments)
  {
    return arguments.Where(argument => argument.Value != null).ToDictionary(argument => argument.Key, argument => argument.Value);
  }

  /// <summary>
  /// Agent API tool update_actual.
  /// </summary>
  [McpServerTool(Name = "update_actual", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
  [Description(
    "Change a transaction's amount, description or date. The transaction is found by its current description under the item; "
      + "a combined transaction's member stays combined, so change its date only together with the other members. "
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
      + SaveHint
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
      + "Use the account's id as accountLinkId and a category's id as accountCategoryLinkId when calling add_actual, "
      + "and only for an account other than the item's default account and the budget's own account. "
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
