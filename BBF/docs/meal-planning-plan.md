# Meal Planning & Shopping — Planning Doc

## Assumptions (flag if wrong)
- Follows the same conventions as the budget module: household-shared via the existing `UserGroup`/`UserGroupMember` multi-tenancy, `IDbContextFactory<ApplicationDbContext>` for data access, MudBlazor UI, EF Core migrations.
- No Walmart API integration in v1 — there is no public consumer API exposing per-store aisle/section data, so the `Section` field on `Ingredient` is the real, permanent system, not a placeholder. Revisit only if a real data source turns up.
- Cross-dimension unit conversion (volume ↔ weight, e.g. cups of flour ↔ lb of flour) is out of scope — it requires per-ingredient density data recipes don't provide. Same-dimension conversion (cup↔tbsp↔tsp, lb↔oz↔g) is fully automatic.
- One purchasable package size per ingredient (not multiple product options per ingredient).
- Meal slots: Breakfast, Lunch, Dinner (no Snack slot).
- Recipes scale by servings; assigning a recipe to a day lets you set a target serving count that scales its ingredient quantities.
- Weeks start on Sunday (arbitrary default — trivial to change to Monday if preferred).

---

## 1. User workflow

**Ingredient & section maintenance** (one-time / as-needed setup)
1. User manages a household list of `GrocerySection`s (e.g. Produce, Dairy, Meat, Bakery, Frozen, Pantry) — add/rename/reorder.
2. User manages `Ingredient`s: name, measurement type (Volume/Weight/Count), section, and optionally a package size (e.g. "5 lb bag") so the grocery list can say "buy 2 bags" instead of just "8 lb needed."

**Recipe entry**
1. User creates a `Recipe`: name, base serving count, instructions (optional), notes.
2. Adds ingredient lines: pick an `Ingredient`, enter quantity + unit, optional prep note ("diced").

**Weekly planning**
1. User opens/creates the current `MealPlanWeek` (7 days × 3 slots: Breakfast/Lunch/Dinner).
2. For an empty slot, opens a recipe picker: search/browse all recipes, with a "Recently Used" shortlist of recipes scheduled in recent past weeks (not an auto-copy of last week — explicitly so the same meal isn't defaulted onto the same weekday).
3. Assigning a recipe to a slot lets the user adjust planned servings (defaults to the recipe's base servings).

**Grocery list**
1. User generates a `GroceryList` for the week from all scheduled `MealPlanEntry` rows.
2. Ingredient quantities are scaled by planned servings, converted to a common unit where the measurement type matches, and summed across all recipes in the week.
3. If an ingredient has a package size configured, the list shows "buy N" (required qty ÷ package size, rounded up) alongside the raw quantity needed.
4. List is grouped by `GrocerySection`, sorted by section order.
5. User can check items off (persisted) and add freeform extra items not tied to any recipe/ingredient (e.g. "paper towels").
6. User can regenerate the list if the week's plan changes — recipe-derived lines are fully recomputed (including checked state, which resets), while manually-added extra items and their checked state are left untouched.

---

## 2. Database schema (SQL Server / EF Core)

```
GrocerySections
  Id (PK)
  GroupId (FK -> UserGroups, nullable, SetNull)
  Name                    -- "Produce"
  SortOrder

Ingredients
  Id (PK)
  GroupId (FK -> UserGroups, nullable, SetNull)
  Name                    -- "All-Purpose Flour"
  MeasurementType          -- enum: Volume, Weight, Count
  SectionId (FK -> GrocerySections, nullable, SetNull)
  PackageQuantity (nullable) -- decimal(10,3), e.g. 5
  PackageUnit (nullable)     -- enum Unit, e.g. Pound

Recipes
  Id (PK)
  GroupId (FK -> UserGroups, nullable, SetNull)
  Name
  BaseServings            -- int
  Instructions            -- nullable text
  Notes                   -- nullable
  CreatedAt

RecipeIngredients
  Id (PK)
  RecipeId (FK -> Recipes, Cascade)
  IngredientId (FK -> Ingredients, Restrict)   -- must remove from recipes before deleting an ingredient
  Quantity                -- decimal(10,3)
  Unit                    -- enum Unit
  Notes                   -- nullable, "diced", "room temperature"

MealPlanWeeks
  Id (PK)
  GroupId (FK -> UserGroups, nullable, SetNull)
  WeekStartDate           -- date (Sunday)
  Unique index (GroupId, WeekStartDate)

MealPlanEntries
  Id (PK)
  MealPlanWeekId (FK -> MealPlanWeeks, Cascade)
  Date                    -- date, one of the 7 days in the week
  MealSlot                -- enum: Breakfast, Lunch, Dinner
  RecipeId (FK -> Recipes, Restrict)          -- must unschedule before deleting a recipe
  PlannedServings         -- int, defaults to Recipe.BaseServings
  Unique index (MealPlanWeekId, Date, MealSlot)

GroceryLists
  Id (PK)
  MealPlanWeekId (FK -> MealPlanWeeks, Cascade, unique)  -- one list per week
  GeneratedAt

GroceryListItems
  Id (PK)
  GroceryListId (FK -> GroceryLists, Cascade)
  IngredientId (FK -> Ingredients, nullable, SetNull)     -- null for freeform extra items
  SectionId (nullable)     -- snapshot of Ingredient.SectionId at generation time, doesn't drift if the ingredient's section changes later
  Description              -- ingredient name, or freeform text for manual items
  RequiredQuantity (nullable) -- decimal(10,3), null for freeform items
  RequiredUnit (nullable)     -- enum Unit
  PackagesToBuy (nullable)    -- int, computed snapshot at generation time
  IsChecked                -- bool, default false
  IsManuallyAdded           -- bool
```

**Unit / MeasurementType** — a fixed enum, not a DB table (unlike sections, which are user-managed):
- `MeasurementType`: Volume, Weight, Count
- `Unit`: Teaspoon, Tablespoon, Cup, Pint, Quart, Gallon, FluidOunce (Volume); Ounce, Pound, Gram, Kilogram (Weight); Each (Count)
- A static conversion table maps each `Unit` to its `MeasurementType` and a factor to a canonical base unit per type (teaspoons for Volume, grams for Weight, each for Count). Conversion/aggregation only happens within the same `MeasurementType`.

**Relationship notes**
- Same `GroupId`-nullable-with-`SetNull` pattern as every other entity in the app (Account, Transaction, etc.) — consistent multi-tenancy.
- `RecipeIngredients.IngredientId` and `MealPlanEntries.RecipeId` both use `Restrict`, matching the existing `RecurringTransaction → Account` pattern: you can't delete something still actively referenced, you have to remove the reference first.
- `GroceryListItems.SectionId` is a **snapshot**, not a live FK to `GrocerySections` — deliberate, so re-sectioning an ingredient later doesn't silently rewrite past grocery lists. Worth confirming this is the behavior you want.
- Watch for the same SQL Server multi-cascade-path issue hit during the budget module (`BudgetCategory` self-reference) — `RecipeIngredients` has two incoming FKs (`Recipes` Cascade, `Ingredients` Restrict), which should be fine since only one path cascades, but worth verifying at migration time.

---

## 3. Blazor Web App organization

```
/Components/Pages
  Recipes.razor              -- route: "/recipes" (list/search, card or table view)
  RecipeDialog.razor         -- add/edit recipe with dynamic ingredient-line rows
  MealPlan.razor             -- route: "/meal-plan" (week grid: 7 days x 3 slots, prev/next week nav)
  MealPlanEntryDialog.razor  -- recipe picker for a slot (search + "Recently Used" shortlist), servings input
  GroceryList.razor          -- route: "/grocery-list" (current week's list, grouped by section, checkboxes, add-item box, regenerate button)
  Ingredients.razor          -- route: "/ingredients" (maintenance: name, measurement type, section, package size)
  IngredientDialog.razor
  GrocerySections.razor      -- route: "/grocery-sections" (maintenance: add/rename/reorder)
  GrocerySectionDialog.razor

/Services
  IngredientService           -- CRUD, section assignment
  GrocerySectionService        -- CRUD, reorder
  RecipeService                -- CRUD recipes + recipe-ingredient lines
  MealPlanService               -- CRUD weeks/entries, recently-used-recipes query
  GroceryListService            -- generate/regenerate, aggregate + convert + sum quantities, compute packages-to-buy, toggle checked, add/remove manual items
  UnitConversion (static helper) -- Unit -> MeasurementType + base-unit factor table, not DB-backed

/Data/Entities
  GrocerySection.cs, Ingredient.cs, Recipe.cs, RecipeIngredient.cs,
  MealPlanWeek.cs, MealPlanEntry.cs, GroceryList.cs, GroceryListItem.cs
```

**NavMenu** — add a grouped section similar to the budget links: Recipes, Meal Plan, Grocery List. Ingredient/Section maintenance pages likely belong under an existing Settings/Admin grouping if one exists, or alongside the other maintenance-style pages.

---

## 4. Page layouts
- **Meal Plan** — 7×3 grid (days × Breakfast/Lunch/Dinner), each cell either shows the assigned recipe (name, servings, quick unassign) or an "Add" affordance opening the recipe picker. Week navigation (prev/next/jump-to-date) at the top.
- **Recipe picker (dialog)** — search box, full recipe list, and a distinct "Recently Used" section pulling recipes from recent past weeks for the same slot type.
- **Recipes** — list/search view; each recipe opens the edit dialog with dynamic ingredient rows (ingredient picker + qty + unit + notes).
- **Grocery List** — grouped by section (in section sort order), each line shows ingredient name, total quantity + unit, "buy N" callout when a package size is configured, and a checkbox. Freeform "add item" input at top or bottom. "Regenerate" button re-syncs from the current meal plan.
- **Ingredients / Grocery Sections** — simple CRUD maintenance tables, consistent with how other settings-style entities are managed elsewhere in the app.

---

## Resolved decisions
- Household-shared (existing `UserGroup` multi-tenancy), not per-user.
- Breakfast/Lunch/Dinner slots, no Snack.
- Manual recipe entry only (no URL import) for v1.
- Same-dimension unit conversion only; cross-dimension (volume↔weight) is explicitly out of scope.
- One package size per ingredient (not multiple product options).
- User-managed custom `GrocerySection` list, not a fixed enum.
- Grocery list supports persisted check-off state and freeform manual items.
- No bulk "copy last week" — instead, the recipe picker surfaces a "Recently Used" shortlist so recent meals are easy to re-pick without defaulting the same meal onto the same weekday.
- No Walmart API integration in v1 — manual `Section` field is the permanent mechanism, not a stopgap.
- Regenerating the grocery list fully recomputes recipe-derived lines (checked state resets on those); manually-added extra items and their checked state are left alone.
- Weeks start on Sunday.
- An empty `MealPlanEntry` slot is just left unfilled — no explicit "skip" marker; it simply doesn't contribute anything to the grocery list.
