# Jackpot Derby — Unity Game Jam 2026 entry (working title)

You (Claude Code) are building this game almost entirely by yourself through the Unity MCP connection.
Lefika (the owner) is new to Unity. Explain what you need from him in plain steps, and only ask him to do
things you can't do through MCP (dialogs, installs, making 3D models, uploading to itch.io).
Work one milestone at a time. At the end of each: commit, push, and give him a short test checklist.

## Jam facts
- Official Unity Game Jam 2026 on itch.io. Theme: **"Lucky Number"**.
- **Submission deadline: Saturday Nov 14, 2026, 12:00 UTC (7:00 AM New York time). No late entries.**
- Must be made in Unity. Target a **WebGL build** (browser-playable on itch.io).
- Generative AI is allowed but **must be disclosed** on the submission page.

## The game in one paragraph
A 4-car combat arena in a cartoon city block. The player starts in one corner garage; 3 AI cars start in the
others. Every car's number is an **odometer** that rolls like slot-machine reels as it drives. Chests appear
with a hidden lucky number; stop to lock your number and get a TOO HIGH / TOO LOW / CLOSE hint, then ram
the chest with a close enough number to win a **blaster**. Blasters auto-fire at nearby enemies (Squad Busters style)
and give a second health bar; lose it and you're demoted. A Bad Luck Storm shrinks the arena. **Last car standing wins.**

## Controls (keyboard)
- W / Up: accelerate · S / Down: brake, then reverse · A D / Left Right: steer
- Blasters fire **automatically** (see Combat feel) — the player only drives
- Shift: **Turbo** (short boost to speed and fire rate, 6 s cooldown)
- Space: **Jackpot Blast** special (Jackpot cannon only, 8 s cooldown)
- R: unlock your number so it rolls again · Esc: pause

## The odometer number (core luck mechanic)
- Range 1–99, wrapping (99 → 1 and 1 → 99). Starts at a random number.
- Driving forward counts **up** 1 per 3 units travelled; reversing counts **down** at the same rate.
- Displayed as two rolling slot-reel digits (HUD for the player; a small floating number over AI cars).
- **Stopping** (speed < 0.5 for 0.4 s) **locks** the number (padlock icon) and shows a hint against the
  nearest active chest:
  - exact → **LUCKY!** (gold)
  - within ±5 → **CLOSE** (green)
  - otherwise → **TOO HIGH** / **TOO LOW**, plus "WAY" in front if more than 25 off
- A locked number stays locked while you drive (so you can drive to the chest). Press **R** to unlock;
  it then rolls again as you drive. Opening a chest or grabbing a dice pickup also unlocks it.
- Stopping is the gamble: a stopped car is an easy target. Keep that tension.

## Chests
- First chest at 10 s, then a new one every 25 s. Max 2 active. Each has a hidden number 1–99.
- Spawn on open ground away from cars, with a light beam so everyone sees it; off-screen arrow on the HUD.
- **Ram it** to try to open it, using your current number:
  - exact or within ±5 → opens it and upgrades you **one step**: no blaster → **small blaster** →
    **Jackpot cannon**. The Jackpot cannon is only ever an upgrade of the small blaster (you can't skip
    straight to it). Already have the cannon → its blaster bar is refilled.
  - exact (LUCKY!) also gives a **full repair** on top of the upgrade
  - too far off → you bounce off (knockback, no damage) and the chest stays
- Every so often (1 in 4) a **Jackpot chest** (chest_jackpot model) spawns: same upgrade rules, plus a full
  repair even when you're only close.

## Health, blasters, demotion
- Car health: 100. Blaster bar sits on top of it and absorbs damage first.
### Combat feel: like Supercell's Squad Busters
The player only steers; positioning is the skill. Combat is automatic, chunky, readable and juicy.
- **Auto-targeting:** an armed car automatically turns its blaster toward and fires at the nearest enemy
  inside its attack range (shown as a soft circle on the ground under the car). It fires while driving.
  No aiming, no fire button. Leaving the range stops the shooting, so chasing and escaping matter.
- **Projectiles are NOT coins:** chunky glowing cartoon blasts — the small blaster fires fat teal-green
  "lucky bolts", the Jackpot cannon fires bigger gold-and-red bursts. Short bright trails, slight arc.
- **Hits feel big:** white hit flash on the target, squash-and-stretch on impact, a puff/pop effect,
  floating damage numbers that pop up and fade, small screen shake when the player is hit or hits.
- **Chunky health bar** floating above a car only while it's taking damage, fading out ~1.5 s after the
  last hit. Only one bar shows: the outermost layer the car still has (Jackpot cannon bar → small blaster
  bar → car health). When a layer is shot away, the next one down takes its place.
- **Turbo (Shift):** 2 s of +40% speed and +50% fire rate, 6 s cooldown, speed lines and a whoosh.
- **Jackpot Blast (Space, Jackpot cannon only):** a big area blast in front of the car, 25 damage,
  knockback, 8 s cooldown, a slot-reel "777" flash on screen.
- Small blaster: bar 50, range 12 units, 1 bolt every 0.5 s, 8 damage.
- Jackpot cannon: bar 80, range 15 units, a 3-burst volley every 0.7 s, 7 damage each.
- Blaster bar hits 0 → **demoted** one tier (Jackpot → small with a 30 bar, small → none).
- **Ramming:** damage to the hit car = relative speed × 1.2 (side or rear hits × 1.5); the rammer takes 30%
  of that. No damage below a small speed threshold. Unarmed cars must flee, hide, or ram.
- Health 0 → eliminated (explosion of coins, car becomes a wrecked_car prop). Last car alive wins.

## Superstitions and pickups
- **Black cats** (2–3) wander the streets. Touching one scrambles your number (random ±20) and unlocks it.
- **Dice pickup** (die model): re-rolls your number to a random 1–99 and unlocks it. A gamble.
- **Clover pickup**: +30 car health. One every 40 s, max 2 on the map.

## Bad Luck Storm
- Starts shrinking at 60 s, every 30 s, toward a point near the centre, down to about 30×20 units by 4 min.
- Outside it: 5 damage per second, dark clouds and a purple tint. Match target length: 3–5 minutes.

## AI opponents (3)
Use NavMesh (AI Navigation package) with a simple state machine: Wander, SeekChest, Hunt, Flee, Ram.
- They play the number game too: drive to adjust, stop to check the hint, adjust again (like a guessing
  search) with some deliberate mistakes so the player can win chest races about half the time.
- Armed → Hunt the weakest visible car. Unarmed and an armed car nearby → Flee around buildings.
  Close and the target is weak or showing its side → Ram.
- Personalities:
  - **Lucky 8** (enemy_car_red): aggressive hunter, always goes for chests.
  - **Black Cat 13** (enemy_car_purple): sneaky, ambushes from corners, loves side rams.
  - **Double Dice 11** (enemy_car_orange): chaotic gambler, grabs dice pickups, takes risks.
- The player's car is **Lucky 7**.

## Arena layout
**Follow `Docs/map_reference.png`** (Lefika's overview image): a big, dense, isometric-looking cartoon city
grid with a garage in each corner and the Bad Luck Storm clouds around the edge.
- Rectangle about 200 × 140 units, one ground level (no drivable overpasses or tunnels for the jam),
  walled with arena_wall / retaining_wall segments; tunnel_entrance and overpass_pillar as edge scenery.
- Streets: a grid of 2-lane roads with intersections, T-junctions and a few curves, plus a central
  roundabout. Build road surfaces, sidewalks and curbs in Unity (planes/ProBuilder) using the textures in
  Assets/Textures: tex_road_straight, tex_road_intersection, tex_road_tjunction, tex_road_curve,
  tex_sidewalk, tex_retaining_wall. Roads must line up perfectly and be wide enough for two cars.
- Corners: garage_lucky7 (bottom-left, player), garage_lucky8 (top-left), garage_blackcat13 (top-right),
  garage_doubledice11 (bottom-right). Each car starts inside its own garage, facing out.
- City blocks between the roads, packed with buildings (apartments, storefronts, houses and their colour
  variants), with rooftop dressing (AC units, water towers, vents, antennas, billboards, neon signs)
  scattered on flat roofs so repeats don't look identical.
- Centre: plaza / roundabout with **lucky_tower** as the landmark. Place casino, lantern_gate, fortune_booth
  and other landmarks so each district is recognizable.
- Street clutter for cover: parked cars, wrecks, dumpsters, trash cans, pallets, tires, drums, crates,
  barriers, fences, benches, kiosks, phone booths, plants, bushes, trees, utility poles, lamps, lights.
- Leave open squares and parking lots for chests and dogfights; every garage needs two exit routes.
- Big map + WebGL: reuse a small set of models many times, mark scenery static, cull or simplify what's far
  from the camera, and keep checking the WebGL frame rate.

## Assets (Assets/Models, GLB from Tripo AI; glTFast imports them)
Gameplay: car, enemy_car_red, enemy_car_purple, enemy_car_orange, blaster_small, blaster_jackpot,
chest, chest_jackpot, black_cat, clover_pickup, arena_wall, coin (decor/elimination burst), die (pickup),
trophy (win screen), slot_machine (menu/decor), finish_arch (arena entrance banner).
Garages: garage_lucky7, garage_lucky8, garage_blackcat13, garage_doubledice11.
Road kit models: overpass_pillar, guardrail, tunnel_entrance, retaining_wall (textures listed above).
Buildings: apartment_cream, apartment_brick, apartment_corner, store_cafe, store_pawn, store_laundromat,
store_mechanic, store_grocer, house_tall, house_small, arcade_building, charm_shop, casino, lucky_tower,
lantern_gate, fortune_booth (+ colour variants with suffixes like _teal, _yellow).
Rooftop: roof_ac_unit, roof_water_tower, roof_billboard, roof_vents, roof_antennas, neon_sign_dice,
neon_sign_clover.
Vehicles (parked): parked_sedan, parked_van, city_bus, taxi, wrecked_car (+ wreck variants).
Street props: pole_wood, pole_concrete, transformer_box, fence_wood, pallet_stack, dumpster, trash_cans,
metal_drums, tire_stack, park_bench, potted_plants, news_kiosk, bush_large, bush_small, tree_twisted,
hotdog_cart, crates_dice, barrier, billboard, tree_planter, phone_booth, traffic_light, bus_stop,
vending_machine, street_lamp, trash_bags, fire_hydrant, mailbox, oil_barrel, traffic_cone (may be cone).
Old road game pieces (road_slab and variants) are optional decoration only.
- Models arrive over time. **Use placeholder primitives for anything missing** and swap later.
- Normalize scale on import: a car is about 2 × 4 units. Mount blasters on the car roofs.
- Use simple Box/Capsule colliders, never MeshColliders on these high-poly models.
- Art style: chunky hand-painted cartoon, slightly worn; teal / yellow / cream / red palette.

## Presentation
- High three-quarter top-down camera, Squad Busters style (matching Docs/map_reference.png): follows
  the player smoothly, high enough to see attack ranges and nearby enemies around buildings.
- HUD: car health + blaster bar, odometer reels with padlock, hint popups, chest arrows, enemy health bars
  over cars, kill feed, storm timer, match timer.
- Juice: hit flashes, screen shake, coin bursts, chest light beams, demotion "BUSTED" popup, LUCKY! popup.
- Results screen: placement (1st–4th), trophy for 1st, wins counter saved with PlayerPrefs, Play Again.
- Main menu: title, Play, How to Play. Stretch: a spinning Earth that zooms into the arena.

## Build order / milestones
1. Setup: URP, WebGL platform + itch.io settings, glTFast, folders, models, git + private GitHub repo.
2. Graybox arena + arcade car controller + chase camera. Driving must feel fun.
   Then a **WebGL test build**.
3. Odometer number, locking, hints, chests, getting blasters.
4. Combat: Squad Busters-style auto-fire, Turbo, Jackpot Blast, health + blaster bar, demotion,
   ramming, elimination, win/lose, restart.
5. AI opponents with personalities.
6. Storm, black cats, dice, clovers; tune match length.
7. Real models, enemy cars, blasters on roofs, full arena dressing, lighting + post-processing; perf check.
8. HUD polish, juice, sounds (free CC0 or generated), main menu, How to Play.
9. Final WebGL build + itch.io upload text.

## Technical rules
- Unity 6, URP, C#. Small readable scripts. Tunable numbers as serialized fields or ScriptableObjects.
- WebGL-safe only: no threads, no file writes (PlayerPrefs), no blocking calls.
- After script changes, read the Unity console via MCP and fix errors before moving on.
- Textures: 2K max for big buildings, 1K for props and cars. Aim for under ~600k triangles on screen.
  Mark static scenery static for batching.
- WebGL player settings: Gzip (or Brotli) with **Decompression Fallback** on, fixed canvas ~1280×720.
- Commit and push after every milestone.

## Submission checklist
- WebGL build zipped (index.html at the zip root), uploaded to itch.io as playable in browser.
- Page: short pitch, controls, how the lucky number works, and the AI disclosure:
  "Code written with Claude (Anthropic). Concept images made with Google Nano Banana. 3D models made with
  Tripo AI. Game design, direction and asset selection by Lefika."
- Submit before Saturday Nov 14, 12:00 UTC.
