# Regression checks

Run from the game directory with the .NET 9 SDK or newer:

```powershell
dotnet run --project 'Plugin Development\CustomItemLoader\Tests\RegressionChecks.csproj' -c Release -- (Get-Location).Path
```

The harness links the production equipment-snapshot, basic-modifier, armor
ledger/persistence, asset-import, preview-identity, ammunition-guard, startup
lifecycle, public readiness, and furniture-lighting code.
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

Lifecycle coverage includes host-independent scene completion, an already-loaded
menu fallback, early and repeated signals, later-plugin item registrations,
reentrant and failing subscribers, late subscribers, optional preparation failure,
and idempotent equipment-save registration. Compiled-assembly guards verify that
bootstrap `OnDestroy` only logs, slot callbacks are static, and readiness no
longer starts a coroutine. Scene and menu dispatch are simulated, not live Unity.

Furniture-lighting checks cover the native shared-material resource, cached
reuse, pickup-highlight preservation during sprite refresh, explicit restoration,
Unity-style destroyed-object handling, missing-resource warnings/recovery, and
compiled creation/load/refresh call sites. The game DLL is checked for the same
pickup diffuse/unlit resource names. These checks do not render actual lighting.

Pack-folder checks cover pinned draft assets, saved folder moves, arbitrary
addon cache variants without a GLB, rename-back cache identity, atomic JSON
backups, write-failure rollback, external edits, destination collisions, shared
and nested packs, root containment, case-only renames, loose JSON files, grouped
packs, reserved Windows names, and invalid/cyclic cache metadata.

Editor smoke checks for 3.0.8: import art into a new pack, change its Pack ID,
save, and restart. Confirm the artwork stays available under the final folder.
Repeat with a cached-only GLB pack and a destination that already exists.
Open through Modding Tools; exercise all four
tabs and the native file picker; drag Front and Back to different positions,
save/restart, rotate the placement cursor and placed furniture; confirm offsets
match. Repeat with a single PNG, cached four-view art with its GLB absent,
and inherited base-game visuals. Check 1280×720 and 1920×1080 layouts.

These are not live Unity integration tests. Before publishing, smoke-test a
backup save in game:

The 3.0.7 run includes 165 checks with the game-directory argument. Layout
budgets and binary guards verify bounded columns, stacked field labels,
text-measured action buttons, selector width/height measured with the drawing
style, and a Refresh preview button outside the scroll area. They do not
inspect rendered pixels. Confirm Save Pack, Change, Clear, Browse, arrows,
Miscellaneous, Automatic, and long add-on slot names stay readable at
1280×720 and 1920×1080. Live Unity verification remains outstanding for this
build; passing these checks is not a substitute for that smoke test.

Text-input checks cover bounded width, adjacent button reservations, explicit
content height, trailing blank lines, shrinking after deletion, and single-line
numeric controls. In game, paste several paragraphs into Description, add empty
lines at the end, then delete most of the text. Confirm the box grows/shrinks and
pushes the next section down without overlap. Repeat with a long display name,
unbroken asset path next to Browse, clone search next to Clear, and the Pack ID
in the sidebar. Paths and IDs must only wrap visually, with no stored newlines.

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
7. Cold-start with Growables and a slot-providing add-on installed. After the
   startup-host-destroyed message, confirm a single CIL registration-complete
   message and Growables' registration count. Enter a save and actually plant
   a custom seed. Return to menu and load again: no duplicate registration or
   planting components. Verify equipment state still saves/restores through the
   Modding Tools companion file. Also test without Growables and with no packs.
8. Place generated PNG and GLB/cache-only furniture beside native furniture.
   Compare both near a lamp and away from it, including at night. Toggle pickup
   highlighting on/off and rotate the custom furniture; let a pending GLB render
   finish while highlighted. The highlight must remain until the game disables
   it, then return to normal diffuse lighting. Save/reload existing generated
   furniture and repeat. No artwork rebake or pack edits should be required.

Keep the native save and its `.moddingtools` companion together. An older
save cannot tell this update which bonuses an earlier pack version applied.
