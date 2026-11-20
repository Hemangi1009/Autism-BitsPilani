CHARACTER WAYPOINT WALKER

EDIT PERCY'S EXISTING ROUTE
Select Child_Precy_Play and find Character Waypoint Walker.
Expand the separate hierarchy object "Child_Precy_Play - Route (move numbered points)".
Move its numbered empty objects with Unity's Move tool. Keep them on clear ground.
Select the character to see route lines and numbered labels in Scene view (Gizmos on).
The ordered Points list determines the route. The last point leads back to the first when Loop is enabled.
Travel means how the character moves TO that point. Wait Seconds means how long it stands AFTER arriving there.
Wait X/Y are minimum/maximum seconds; use 0/0 for no stop.
Optional Look At sets the facing target during a pause.
Travel Clip / Idle Clip override the standard clips at that point only.

USE ON ANOTHER CHARACTER
1. Add Character Waypoint Walker to the character root containing its Animator.
2. Assign that character's compatible Standing Idle, Walk, and Run clips.
3. Set Example Route Radius, open the component's context menu, and choose "Create Example Route (empty route only)".
4. Move the generated points into a suitable clear route. The helper never overwrites an existing Points list.
5. Assign Ground Terrain (or one Ground Collider) and set Ground Offset for that model's foot placement.
6. Adjust walk/run speeds, acceleration, turning speed, blend duration and per-point pauses.

ANIMATION AND PERFORMANCE
The original clips and Animator Controller assets are never edited.
A small runtime Playables mixer blends two clips at a time and repeats playback without changing clip settings.
The original Animator Controller is temporarily detached during route playback so its default clip cannot override Walk/Run/Idle. It is restored when the route is disabled. The controller asset is never edited.
Apply Root Motion is temporarily disabled while the component controls movement, then restored when disabled.
Walk/Run Clip Metres Per Second controls playback-rate matching. Adjust if feet slide.
No navigation bake or external package is required. This follows authored points; it does not find a route around newly placed obstacles. Keep point-to-point legs clear when editing.
Terrain height sampling avoids scene-wide physics queries. If using a Ground Collider, only that collider is queried.
No per-frame object searches, route rebuilding or diagnostic logging. Enable Log Arrivals only when needed.
The component's PauseRoute, ResumeRoute and RestartRoute methods can be called from Inspector events.
RestartRoute travels toward point 1 from the current position; it does not teleport.
