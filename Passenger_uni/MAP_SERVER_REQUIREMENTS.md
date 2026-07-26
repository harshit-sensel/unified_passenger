# Map page (index-min.html) – requirements for Passenger Pro

The app opens the map at:
`http://demo.vehicletrackingsolution.co.in/hybridapp_pages/index-min.html`

It sends these query parameters. The **server-side map page** should use them as follows.

## 1. Zoom (already sent by app)

- `defaultZoom=17` – use as initial map zoom.
- `zoom=17` – same.
- `scaleMeters=50` – target scale ~50 m so the map does not go to 20 m (white tiles) and does not jump to 100 m after refresh.

**Action:** On load and on each refresh, set map zoom to 17 (or equivalent ~50 m scale). Do not auto-zoom to 20 m on location update.

---

## 2. Blue route line (server must implement)

- `drawRoute=1`
- `showRouteLine=1`

**Action:** Draw the vehicle’s route as a **blue polyline** on the map.

- Use the position history for the current vehicle/session (from your API) as the polyline points.
- Prefer **continuous drawing**: as new positions arrive, append them to the polyline so the route grows along the path (user-friendly).
- Line style: blue, clearly visible (e.g. 4–6 px width).

---

## 3. Smooth vehicle movement (server must implement)

- `animateVehicle=1`

**Action:** When the vehicle position updates (e.g. on refresh or push):

- **Do not** snap the vehicle marker from old position to new position (that causes the “jump”).
- **Do** animate the marker from the previous lat/lng to the new lat/lng over a short duration (e.g. 0.5–1.5 s) so the movement looks smooth.

---

## Summary

| Param             | Purpose                          | Server action                                      |
|------------------|-----------------------------------|----------------------------------------------------|
| defaultZoom, zoom, scaleMeters | Stable 50 m zoom, no 20 m / 100 m jump | Set initial and post-refresh zoom to 17 (~50 m).   |
| drawRoute, showRouteLine | Blue route line                  | Draw blue polyline from position history; append as vehicle moves. |
| animateVehicle   | No jumping of vehicle icon        | Animate marker from old to new position.           |

The Android app cannot draw on the map or change map behaviour; all of the above must be implemented in the map page (index-min.html) and any APIs that provide position history.
