# Sports VR Tour – Unity (Meta Quest 3)

Same tour as the web version: 360° videos, floating section menu, "YOU ARE AT" label,
hotspot arrows inside each video, look-down-to-open menu, background music.

## Open the project
1. **Unity Hub → Add → Add project from disk →** select this `SportsVRTour-Unity` folder.
2. Open it with **Unity 6000.0.80f1** (Android Build Support must be installed – it is).
3. Open `Assets/Scenes/SportsTour.unity`.

If the scene is missing, or after changing settings by mistake, use the top menu:
- **Sports Tour → 1. Apply Meta Quest 3 Settings** (Android, IL2CPP/ARM64, OpenXR + Meta Quest feature)
- **Sports Tour → 2. Build Tour Scene**

## Try it in the Editor
Press **Play**. Drag mouse to look around, click menu items / arrows.
Keys: hold **W / Space / ↑** to walk · **M** menu · **1–9** jump to section · **H** print look direction (yaw/pitch) to the Console + clipboard.
On Windows with Quest Link, Play mode runs in the headset too. On a Mac it always runs in mouse mode (Meta has no Quest Link for macOS).

## Put it on the Quest 3
1. Quest must be in **Developer Mode** (Meta Horizon phone app → Devices → Developer Mode).
2. Connect Quest to the Mac with USB-C, put it on, allow **USB debugging**.
3. **File → Build Profiles → Android →** your Quest should show under *Run Device* → **Build And Run**.
4. Later you find it on the Quest under **Library → Unknown Sources → Sports VR Tour**.

The videos are packed **inside the APK** (~250 MB), so the app works on any Quest on its own.
They are compressed copies of `../videos` (HEVC, same 3072×1536 resolution, ~24 Mbps) –
the originals (~1 GB) made the build fill the disk.

### Changing / adding a video
Put the new `.mp4` (equirectangular 360°) in `Assets/StreamingAssets/Videos`, ideally under ~40 MB per 10 s,
and add or update its entry in **Sections**.

## In the headset
The video does **not** move by itself (that caused dizziness): you stand still in a 360° view and only
move forward through the video while you hold the trigger. The edges darken a little while walking (comfort vignette,
strength in the Inspector → Walking). At the end of a path, pick an arrow to go to the next section.

| Action | How |
|---|---|
| **Walk** | **Hold the trigger** (index finger) anywhere except a button – press harder to walk faster. Let go = stand still |
| Open menu | Look **down** at the MENU pad ~1 s, or press **A / X** |
| Select | Point the laser + **trigger** (no controllers: look at it for 1.4 s) |
| Go to next section | Point at a glowing arrow in the video |
| Music on/off | Button at the bottom of the menu |

## Edit sections & arrows
Select the **Sports Tour** object in the Hierarchy → Inspector → **Sections**.
- `Video File` = file name in `Assets/StreamingAssets/Videos`
- `Links` = arrows: `To` (section id), `Yaw` (0 ahead, 90 right, -90 left, 180 behind), `Pitch` (negative = lower)

The arrow directions are placeholders. To place one: Play, look at the spot in the video (e.g. the court gate),
press **H**, and copy the yaw/pitch from the Console into that link.

## Files
- `Assets/SportsTour/Scripts/TourManager.cs` – menu, label, arrows, video switching, music
- `Assets/SportsTour/Scripts/VRRig.cs` – head tracking, controller lasers, gaze + mouse fallback
- `Assets/SportsTour/Scripts/TourData.cs` – section list (defaults)
- `Assets/SportsTour/Scripts/Editor/SportsTourBuilder.cs` – the "Sports Tour" menu (scene + Quest settings)
- `Assets/SportsTour/Audio/TourMusic.wav` – background music loop
- `Assets/StreamingAssets/Videos/` – the 360° videos packed into the app
