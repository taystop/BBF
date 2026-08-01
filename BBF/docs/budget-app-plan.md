# Personal Budgeting Site — Planning Doc

## Assumptions (flag if wrong)
- .NET 9, Blazor Web App (server render mode to start, WASM-ready later if needed)
- SQL Server + EF Core 9 for data access
- Being added as a module into an existing site — reuses existing auth/user store rather than standing up a new one; routes and layout below should nest under whatever area convention the existing site already uses
- Multi-user households from day one: a household is the sharing boundary for accounts, categories, budgets, and goals; a user can belong to a household and see/edit its shared data
- A separate system-admin page assigns users to households (distinct from in-household roles)
- Rollover default: category balances reset each month — unspent amounts do not carry forward, unless a category is explicitly configured otherwise
- Bank sync targets Capital One specifically (likely via Plaid or Capital One's own developer API — worth a quick spike to confirm current OAuth/consent flow before committing)
- UI layer is MudBlazor, matching the existing site
- Methodology: hybrid of zero-based budgeting (YNAB-style) + envelope visualization (Goodbudget-style), since those were the two patterns you liked most

---

## 1. User workflow

**Onboarding**
1. Register / log in (existing site auth)
2. A system admin assigns the user to a household via the admin page, or the user creates a new household and becomes its first member
3. Create one or more accounts (checking, savings, credit card) with starting balance, optionally linked to Capital One via bank sync
4. Set up income sources and a first budget period (monthly)
5. Create categories (or accept starter templates: Housing, Food, Transportation, etc.) and assign each a budgeted amount — every dollar of income gets assigned somewhere (zero-based)

**Admin**
1. System admin opens the household management page
2. Views all users and their current household assignment
3. Assigns/reassigns a user to a household, or creates a new household

**Daily / weekly use**
1. Log in → Dashboard shows net worth, this month's budget-vs-actual, and a Pace-style "on track / overspending" indicator per category
2. Add a transaction (manual entry to start — bank sync is a v2 feature) → assign to account + category
3. Recurring transactions (rent, subscriptions) auto-generate on schedule and just need confirming
4. Envelope view shows each category as a "envelope" with remaining balance, color-shifting as it depletes

**Monthly cycle**
1. Month closes → unspent category balances either roll over or reset (user-configurable per category)
2. Review screen: budgeted vs actual vs prior month
3. Set next month's budget (copy forward + adjust)

**Goals (stretch feature)**
1. Create a savings goal (e.g. "Emergency fund: $5,000") tied to an account
2. Track progress with target date and contribution history

---

## 2. Database schema (SQL Server / EF Core)

Assume `Users` already exists on the host site — the tables below reference it by Id but don't redefine it. Sharing boundary is `Households`; a user's household membership (not the user record itself) is what's admin-managed.

```
Households
  Id (PK)
  Name                   -- "The Smith household"
  CreatedAt

HouseholdMembers
  Id (PK)
  HouseholdId (FK -> Households)
  UserId (FK -> existing Users table)
  Role                    -- enum: Owner, Member
  JoinedAt

-- System-level admin flag lives wherever the existing site tracks roles/claims;
-- referenced here as UserId having an "app admin" claim, not a new table.

Accounts
  Id (PK)
  HouseholdId (FK -> Households)
  Name                  -- "Capital One Checking"
  AccountType            -- enum: Checking, Savings, CreditCard, Cash, Investment
  StartingBalance        -- decimal(18,2)
  CurrentBalance          -- decimal(18,2), maintained via transactions
  IsArchived
  CreatedAt

BankConnections
  Id (PK)
  HouseholdId (FK -> Households)
  AccountId (FK -> Accounts)
  Provider               -- "CapitalOne" (or "Plaid" if routed through an aggregator)
  ExternalAccountId       -- provider-side account identifier
  AccessTokenRef          -- reference to securely stored token, not the token itself
  LastSyncedAt
  SyncStatus              -- enum: Connected, Error, Disconnected

Categories
  Id (PK)
  HouseholdId (FK -> Households)
  Name                  -- "Groceries"
  ParentCategoryId (FK -> Categories, nullable)   -- for grouping, e.g. "Food" > "Groceries"
  Icon                  -- optional, string key
  RolloverBehavior       -- enum: Reset (default), RollOver
  IsArchived
  SortOrder

BudgetPeriods
  Id (PK)
  HouseholdId (FK -> Households)
  StartDate
  EndDate
  Status                -- enum: Open, Closed

BudgetLineItems
  Id (PK)
  BudgetPeriodId (FK -> BudgetPeriods)
  CategoryId (FK -> Categories)
  BudgetedAmount         -- decimal(18,2)
  RolledOverAmount        -- decimal(18,2), computed at period close; 0 for Reset categories

Transactions
  Id (PK)
  HouseholdId (FK -> Households)
  AccountId (FK -> Accounts)
  CategoryId (FK -> Categories, nullable for transfers)
  CreatedByUserId (FK -> existing Users table)   -- who logged it, for household audit trail
  Amount                 -- decimal(18,2), signed (+income, -expense)
  TransactionDate
  Payee
  Notes
  IsTransfer
  TransferAccountId (FK -> Accounts, nullable)   -- other side of a transfer
  RecurringTransactionId (FK -> RecurringTransactions, nullable)
  ExternalTransactionId (nullable)                -- dedupe key for bank-synced transactions
  CreatedAt

RecurringTransactions
  Id (PK)
  HouseholdId (FK -> Households)
  AccountId (FK -> Accounts)
  CategoryId (FK -> Categories)
  Amount
  Payee
  Frequency              -- enum: Weekly, BiWeekly, Monthly, Yearly
  NextOccurrence
  EndDate (nullable)
  IsActive

Goals
  Id (PK)
  HouseholdId (FK -> Households)
  AccountId (FK -> Accounts, nullable)
  Name                   -- "Emergency fund"
  TargetAmount
  TargetDate (nullable)
  CreatedAt

GoalContributions
  Id (PK)
  GoalId (FK -> Goals)
  TransactionId (FK -> Transactions, nullable)
  Amount
  ContributedAt
```

**Relationship notes**
- Everything shared (accounts, categories, budgets, transactions, goals) hangs off `HouseholdId`, not `UserId` — that's what makes it a shared household budget instead of per-user silos. `CreatedByUserId` on `Transactions` keeps a per-user audit trail without scoping data access to the individual.
- `HouseholdMembers` is the only place user-to-household assignment lives, which is exactly what the admin page manages — add/remove/re-role a `(UserId, HouseholdId)` pair.
- `Categories.ParentCategoryId` self-references for a two-level grouping (group → category), so your envelope UI can show grouped sections.
- `BudgetLineItems` is the join between a period and a category — this is what makes zero-based budgeting work: sum of `BudgetedAmount` per period should equal planned income. With the Reset default, `RolledOverAmount` will be 0 for nearly every category unless one is explicitly flagged RollOver.
- `Transactions.CategoryId` nullable specifically for transfers between the household's own accounts, which shouldn't count against a budget category.
- `BankConnections` stores a reference to the token, not the token itself — actual secret storage should go wherever the existing site already keeps credentials (key vault, protected config), not a plain column.
- `Transactions.ExternalTransactionId` gives the sync job a dedupe key so re-pulling Capital One transactions doesn't create duplicates.
- `CurrentBalance` on `Accounts` is a denormalized running total — maintained by a trigger, computed column, or recalculated on write in application code (your call based on how much you trust EF to keep it in sync).

---

## 3. Blazor Web App organization

```
/Components
  /Layout
    MainLayout.razor          -- nav sidebar + top bar
    NavMenu.razor
  /Pages
    Dashboard.razor            -- route: "/"
    Accounts/
      AccountList.razor        -- route: "/accounts"
      AccountDetail.razor      -- route: "/accounts/{id}"
    Budget/
      BudgetOverview.razor     -- route: "/budget" (envelope grid, current period)
      BudgetPeriodHistory.razor -- route: "/budget/history"
    Transactions/
      TransactionList.razor    -- route: "/transactions"
      TransactionForm.razor    -- shared add/edit component, used as a modal/drawer
    Recurring/
      RecurringList.razor      -- route: "/recurring"
    Goals/
      GoalList.razor           -- route: "/goals"
      GoalDetail.razor         -- route: "/goals/{id}"
    Settings/
      CategoryManager.razor    -- route: "/settings/categories"
      ProfileSettings.razor    -- route: "/settings/profile"
      BankConnections.razor    -- route: "/settings/bank-connections" (Capital One linking, sync status)
    Admin/
      HouseholdManagement.razor  -- route: "/admin/households" (list users, assign/reassign to a household), gated by app-admin claim
  /Shared
    EnvelopeCard.razor          -- reusable envelope/category progress card
    AccountBalanceCard.razor
    PaceIndicator.razor         -- PocketGuard-style "on track" widget
    TransactionRow.razor
    CurrencyInput.razor

/Services
  IHouseholdService / HouseholdService   -- membership, admin assignment
  IAccountService / AccountService
  IBudgetService / BudgetService     -- handles period rollover logic (reset by default)
  ITransactionService / TransactionService
  IRecurringTransactionService       -- background job or on-login check to materialize due transactions
  IGoalService / GoalService
  ICapitalOneSyncService / CapitalOneSyncService   -- pulls transactions, dedupes via ExternalTransactionId

/Data
  BudgetDbContext.cs
  /Migrations
  /Entities                          -- matches schema above

/Models
  DTOs / view models separate from EF entities (keeps UI decoupled from schema changes)
```

**Notes**
- A background service (or a check-on-login) walks `RecurringTransactions` where `NextOccurrence <= today` and materializes them into `Transactions`, then advances `NextOccurrence`.
- Period close (`BudgetPeriods.Status = Closed`) is a service method that computes `RolledOverAmount` per category based on `RolloverBehavior` (Reset by default, so this is 0 for most categories), then opens the next period pre-populated from the last one.
- All page-level components and services scope their queries by the current user's `HouseholdId` (resolved via `HouseholdMembers`), not `UserId` — that's the key difference from a single-user design.
- Component names above assume MudBlazor (`MudCard`, `MudProgressLinear`, `MudTable`, `MudDataGrid` for transactions) to match the existing site's UI layer.
- Since this is joining an existing site, treat the `/Pages` and route structure above as a suggested area/module layout to adapt to whatever convention the site already follows, rather than a fixed structure.

---

## 4. Page layouts (see accompanying diagrams for visuals)
- **Dashboard** — net worth summary, this month's budget-vs-actual bar, pace indicator, recent transactions
- **Budget/envelope view** — grouped category cards, each showing budgeted / spent / remaining, color-shifts as it depletes
- **Transactions** — filterable table, add/edit via slide-out form
- **Accounts** — list of account balance cards, drill into transaction history per account
- **Goals** — progress bars toward target amounts

---

## Resolved decisions
- Multi-user households from the start, with an admin page for assigning users to households
- Rollover default: reset (no carryover) unless a category is explicitly set to roll over
- Bank sync targets Capital One
- UI layer: MudBlazor, matching the existing site
- This is a module added to an existing site, so exact routes/structure above are a starting point for Claude Code to adapt, not a fixed spec
