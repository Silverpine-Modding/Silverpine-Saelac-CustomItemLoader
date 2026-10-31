# Regression checks

Run from the game directory with the .NET 9 SDK or newer:

```powershell
dotnet run --project 'Plugin Development\CustomItemLoader\Tests\RegressionChecks.csproj' -c Release -- (Get-Location).Path
```

The harness links the production equipment-snapshot, basic-modifier, armor
ledger/persistence, asset-import, preview-identity, and ammunition-guard code.
It uses small game/Modding Tools/Harmony contracts for headless execution.
The bundled Harmony targets Unity Mono and cannot execute in the .NET 9 host.
Cecil checks the actual installed game's method signatures and serialization
call sites. No real save or pack is opened or changed by these tests.

Coverage includes cap/unequip round trips, multiple capped bonuses, negative
bonuses, player and non-player armor serialization, old saves, pack modifier
changes across load, changed/non-equipable/conflicting slots, clothing snapshots,
missing/incompatible/empty ammunition, forward ammo references, image import
collisions, GLB cache identities, and stale preview identities. The production
per-view offset and shared-library code is also exercised: independent/reset
directions, JSON duplication, finite/bounded offsets, pivot math, art deduplication,
original GLB names, invalid manifests, and GLB-free pack copies.

Editor smoke checks for 3.0.4: open through Modding Tools; exercise all four
tabs and the native file picker; drag Front and Back to different positions,
save/restart, rotate the placement cursor and placed furniture; confirm offsets
match. Repeat with a single PNG, cached four-view art with its GLB absent,
and inherited base-game visuals. Check 1280×720 and 1920×1080 layouts.

These are not live Unity integration tests. Before publishing, smoke-test a
backup save in game:

The 3.0.4 run includes 111 checks with the game-directory argument. Layout
budgets and binary guards verify bounded columns, stacked field labels,
text-measured action buttons, selector width/height measured with the drawing
style, and a Refresh preview button outside the scroll area. They do not
inspect rendered pixels. Confirm Save Pack, Change, Clear, Browse, arrows,
Miscellaneous, Automatic, and long add-on slot names stay readable at
1280×720 and 1920×1080. Live Unity verification remains outstanding for this
build; passing these checks is not a substitute for that smoke test.

1. Equip/unequip armor that crosses 254, save/reload, then unequip. Repeat with
   a resistance effect expiring and with multiple armor contributions.
2. Save with custom gear equipped, edit its basic modifiers, clothing defense,
   and slot in the pack, restart, and reload. Check slot migration and exact
   stat reversal; re-equip to apply the new settings.
3. Repeat with an occupied target slot and a newly non-equipable item. Confirm
   both items remain in inventory and only the valid occupant stays equipped.
4. Render a turnable GLB, then switch item/pack, edit settings, clear the
   preview, or close the editor while rendering. No stale PNGs should publish.
5. Load an unambiguous legacy cache-only pack without its source GLB. Confirm
   all directions load. An ambiguous legacy name must report a re-render need.
6. Fire a custom ranged weapon with no ammo, wrong-component ammo, zero charges,
   and valid ammo. Check notification/targeting and absence of exceptions.

Keep the native save and its `.moddingtools` companion together. An older
save cannot tell this update which bonuses an earlier pack version applied.
