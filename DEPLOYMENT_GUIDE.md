# PediatricVR — Deployment Guide
**Unity 6000.3.17f1 · Meta Quest 3S · WebSocket Remote Control**

---

## Honest Note on 3D Assets

You do not have any fish models. You need them before any of the scene scripts are meaningful. See **§ 9 — 3D Assets** for exactly what to source and where. Do not skip it and wonder why the scene is empty.

---

## Contents

1. [System Requirements](#1-system-requirements)
2. [Unity Project Setup](#2-unity-project-setup)
3. [Meta XR SDK Installation](#3-meta-xr-sdk-installation)
4. [WebSocketSharp Installation](#4-websocketsharp-installation)
5. [Scene Hierarchy Setup](#5-scene-hierarchy-setup)
6. [Inspector Wiring — All References](#6-inspector-wiring--all-references)
7. [Android Build Configuration](#7-android-build-configuration)
8. [Deploying to Quest 3S](#8-deploying-to-quest-3s)
9. [3D Assets — What to Source and Where](#9-3d-assets--what-to-source-and-where)
10. [Audio Assets](#10-audio-assets)
11. [ADB Commands — Auto-Launch on Boot](#11-adb-commands--auto-launch-on-boot)
12. [Hospital WiFi Configuration](#12-hospital-wifi-configuration)
13. [Nurse Panel Deployment](#13-nurse-panel-deployment)
14. [Testing Checklist](#14-testing-checklist)
15. [Troubleshooting](#15-troubleshooting)
16. [Script Execution Order](#16-script-execution-order)

---

## 1. System Requirements

| Requirement | Version / Spec |
|---|---|
| Unity | **6000.3.17f1** (exact — do not upgrade mid-project) |
| Meta XR All-in-One SDK | 65.0+ (via Package Manager) |
| Android Build Support | Installed via Unity Hub |
| Android SDK API | 32 minimum, 33 target |
| JDK | 17 (bundled with Unity 6) |
| ADB | Included in Android SDK platform-tools |
| OS | Windows 10/11 or macOS 12+ |
| Quest 3S firmware | Latest — update via companion app before dev |

---

## 2. Unity Project Setup

### 2.1 Open Your Existing Project

You already have a base project configured for Meta Quest 3S. Open it.

**Before copying any scripts**, confirm:

```
Edit → Project Settings → Player → Other Settings
  Scripting Backend: IL2CPP          ← MUST be IL2CPP for Quest
  Target Architectures: ARM64        ← Quest 3S is ARM64 only
  API Compatibility Level: .NET Standard 2.1
  Active Input Handling: Both        ← supports old + new Input System
```

### 2.2 Copy Script Files

Copy the entire `Assets/` folder structure from this delivery into your project root. Unity will detect and compile all scripts automatically.

Expected compile errors at this point: **0**  
If you see "The type or namespace 'WebSocketSharp' could not be found" — the DLL is not yet installed. See § 4.

### 2.3 Create the Main Scene

```
File → New Scene → Basic (Built-in)
Save as: Assets/Scenes/Main.unity
```

This is the only scene the app uses.

---

## 3. Meta XR SDK Installation

### 3.1 Add Package Registry

##### AR: I removed this because it is creating erorr and we already Meta XR plugin installed
```
Edit → Project Settings → Package Manager → Scoped Registries
Add:
  Name:  Meta XR
  URL:   https://npm.developer.oculus.com
  Scope: com.meta.xr
```

### 3.2 Install SDK

```
Window → Package Manager → My Registries
Install: Meta XR All-in-One SDK (latest stable)
```

This installs OVRCameraRig, OVRManager, hand tracking, etc.

### 3.3 Configure OVR

```
Meta → Tools → Project Setup Tool
Run all Fix buttons for Android / Quest 3S
```

Critical settings the Project Setup Tool enforces:
- Color space: Linear
- Graphics API: OpenGLES3 (Vulkan optional but less stable on Quest 3S)
- Framerate: 72Hz minimum (90Hz target)
- Quest Features → Passthrough: disabled for this app (fully immersive)

---

## 4. WebSocketSharp Installation

See `Assets/Plugins/WebSocketSharp/INSTALL.txt` for full instructions.

**Quick version:**
1. Download from NuGet: `WebSocketSharp-NonPreRelease`
2. Extract DLL from `.nupkg` → rename to `.zip` → open → `lib/net45/websocket-sharp.dll`
3. Place at: `Assets/Plugins/WebSocketSharp/websocket-sharp.dll`
4. In Inspector set platforms: Editor ✓, Android ARM64 ✓
5. Confirm `Assets/Plugins/link.xml` exists (prevents IL2CPP stripping)

---

## 5. Scene Hierarchy Setup

Build this exact hierarchy in your Main scene. Names matter — they're referenced in comments throughout the scripts.

```
Main.unity
│
├── [GameManager]                     ← Empty GameObject
│     Scripts: GameManager, AppBootstrap, AgeProfileManager
│
├── [NetworkManager]                  ← Empty GameObject
│     Scripts: WebSocketServerManager, CommandProcessor
│
├── OVRCameraRig                      ← From Meta XR SDK prefab
│     └── TrackingSpace
│           └── CenterEyeAnchor      ← This is the XR Main Camera
│
├── [UnderwaterScene]                 ← Empty GameObject (root of all VR content)
│     Scripts: UnderwaterController
│     │
│     ├── UnderwaterRoot              ← Parent of all visible underwater objects
│     │     ├── FishSchool_01         ← Scripts: FishSchool
│     │     ├── FishSchool_02         ← Scripts: FishSchool
│     │     ├── FishSchool_03         ← Scripts: FishSchool
│     │     ├── BubbleParticles       ← ParticleSystem
│     │     ├── PlanktonParticles     ← ParticleSystem
│     │     └── CausticProjector      ← ParticleSystem or Projector
│     │
│     ├── SunRayLight                 ← Directional Light
│     │
│     └── GuidedCreature              ← Scripts: HeadOrientationInput
│           (sea turtle mesh — Profile B only — disabled by default)
│
├── [FadeCanvas]                      ← World Space Canvas, child of CenterEyeAnchor
│     └── FadeImage                   ← Image component, full rect, black
│           (CanvasGroup on this GameObject)
│
├── [IdleCanvas]                      ← World Space Canvas, child of CenterEyeAnchor
│     Scripts: IdleScreen
│     └── IdlePanel                   ← Contains all idle screen UI elements
│           ├── NurseUrlText          ← TextMeshProUGUI
│           ├── StatusText            ← TextMeshProUGUI
│           ├── InstructionText       ← TextMeshProUGUI
│           ├── VersionText           ← TextMeshProUGUI
│           └── StatusDot             ← Image (circle)
│
└── [AudioManager]                    ← Empty GameObject
      Scripts: AmbientAudioManager
      Components: AudioSource (x3) — ambient, bubbles, music
```

### World Space Canvas Settings (for both canvases)

```
Canvas component:
  Render Mode: World Space
  Event Camera: (leave empty — no UI interaction needed)

RectTransform:
  Width: 1.8    Height: 1.0    (meters)
  Position relative to CenterEyeAnchor: Z = 2.0 (2 meters in front)

Canvas Scaler:
  Dynamic Pixels Per Unit: 1
  Reference Pixels Per Unit: 100
```

---

## 6. Inspector Wiring — All References

This is where most integration bugs come from. Wire every field.

### GameManager

| Field | Assign |
|---|---|
| Max Session Minutes | 15 |
| Idle Screen | [IdleCanvas] → IdleScreen component |
| Underwater Controller | [UnderwaterScene] → UnderwaterController |
| Profile Manager | [GameManager] → AgeProfileManager |
| Audio Manager | [AudioManager] → AmbientAudioManager |

### AgeProfileManager

| Field | Assign |
|---|---|
| Underwater Controller | [UnderwaterScene] → UnderwaterController |
| Head Orientation Input | GuidedCreature → HeadOrientationInput |
| Audio Manager | [AudioManager] → AmbientAudioManager |

### UnderwaterController

| Field | Assign |
|---|---|
| Fish Schools | [FishSchool_01, 02, 03] — drag all three |
| Underwater Root | UnderwaterRoot GameObject |
| Sun Ray Light | SunRayLight → Light component |
| Bubble Particles | BubbleParticles → ParticleSystem |
| Plankton Particles | PlanktonParticles → ParticleSystem |
| Fade Overlay | FadeImage → CanvasGroup |

### Each FishSchool (x3)

| Field | Assign |
|---|---|
| Fish Prefab | Your fish prefab (see § 9) |
| Fish Count | 8–12 |
| School Radius | 1.5–2.5 |
| Swim Speed | 0.25 (adjust after testing) |
| Waypoint Radius | 5–8 |

### HeadOrientationInput (on GuidedCreature)

| Field | Assign |
|---|---|
| XR Camera | CenterEyeAnchor → Camera component |
| Guided Creature | GuidedCreature Transform |
| Creature Lead Distance | 3.0 |

### AmbientAudioManager (AudioSources on same GameObject)

Add 3 AudioSource components to [AudioManager]:
- AudioSource 1 → assign ocean ambient clip → drag to `Ambient Ocean Source`
- AudioSource 2 → assign bubble clip → drag to `Bubble Source`  
- AudioSource 3 → assign music clip → drag to `Music Source`

Set all three: **Loop = true**, **Play On Awake = false**, **Spatial Blend = 0** (2D)

### IdleScreen

| Field | Assign |
|---|---|
| Idle Panel | IdlePanel GameObject |
| Nurse Url Text | NurseUrlText → TextMeshProUGUI |
| Ws Status Text | StatusText → TextMeshProUGUI |
| Instruction Text | InstructionText → TextMeshProUGUI |
| App Version Text | VersionText → TextMeshProUGUI |
| Status Dot | StatusDot → Image |

---

## 7. Android Build Configuration

```
File → Build Settings
  Platform: Android
  Texture Compression: ASTC
  ✓ Export Project: OFF (direct APK build)

Switch Platform (wait for reimport)

Player Settings (Android tab):
  Package Name: com.pediatricvr.distraction
  Version: 1.0.0
  Bundle Version Code: 1
  
  Minimum API Level: Android 12.0 (API 32)
  Target API Level: Android 13.0 (API 33)
  
  Scripting Backend: IL2CPP
  Target Architectures: ARM64 only
  
  Strip Engine Code: ON
  Managed Stripping Level: Minimal (use Minimal until stable, then High)

Graphics Settings:
  Color Space: Linear
  Auto Graphics API: OFF
  Graphics APIs: OpenGLES3

Quality Settings:
  Rendering → VSync Count: Don't Sync
  (Quest handles its own vsync)
```

### Verify AndroidManifest is Picked Up

After build switch, check:
`Temp/StagingArea/AndroidManifest.xml` (after a build) should contain:
- `android.permission.INTERNET`
- `android:usesCleartextTraffic="true"`
- Both `HOME` and `DEFAULT` intent categories

---

## 8. Deploying to Quest 3S

### 8.1 Enable Developer Mode on Quest 3S

1. Download Meta Quest Developer Hub (MQDH) on your PC
2. Create / log into Meta developer account at developer.oculus.com
3. Create an organisation (required even for solo devs)
4. On Quest 3S: Settings → System → Developer → Enable Developer Mode
5. Restart the headset

### 8.2 Connect via USB

```bash
# Verify connection (run in terminal with Quest connected):
adb devices

# Expected output:
# 1PASH0xxxxxxxxx  device
# (not "unauthorized" — if unauthorized, put on headset and Accept the prompt)
```

### 8.3 Build and Install

**Option A — Unity Build Helper (recommended):**
```
Unity menu: PediatricVR → Build Quest APK
Then:       PediatricVR → Install APK via ADB
```

**Option B — Manual:**
```bash
# Build APK in Unity: File → Build Settings → Build
# Then install:
adb install -r Builds/PediatricVR.apk
```

Build time: ~5–10 minutes first build, ~2–3 min incremental.

### 8.4 Launch and Verify

```bash
# Launch app via ADB without touching headset:
adb shell am start -n com.pediatricvr.distraction/com.unity3d.player.UnityPlayerGameActivity

# Check live logs from Quest:
adb logcat -s Unity:V
```

In the logcat output, look for:
```
[WSS] WebSocket server listening on ws://192.168.x.x:8080/control
[HTTP] Nurse panel served at http://192.168.x.x:8081
```

---

## 9. 3D Assets — What to Source and Where

**Uncomfortable truth: the scripts are scaffolding. Without actual 3D assets, the scene is an empty void.** The fish scripts work, but there is nothing to render. You need meshes before any meaningful testing.

### What You Need

| Asset | Purpose | Priority |
|---|---|---|
| Fish (3 varieties) | FishSchool prefabs (x3 schools) | **Critical — MVP blocker** |
| Sea turtle | GuidedCreature for Profile B | Medium |
| Underwater environment | Seabed, coral, rock geometry | Medium |
| Skybox (underwater) | Background gradient | Low — can use solid colour first |

### Recommended Free Sources

**Unity Asset Store — Free Assets (search exact names):**

1. **"Animated Fish Pack"** by Unluck Software  
   Free tier has 5 fish species, all rigged with swim cycles.  
   Has LODs. Download directly in Package Manager.  
   ⚠ Check licence: free for personal/prototype, paid for commercial release.

2. **"Simple Fish Pack"** by Kenney (Kenney.nl)  
   Low-poly, CC0 licence (fully free including commercial).  
   No animations — add a simple sway animation via Animation clip.  
   Good for MVP testing.

3. **"Underwater Environment"** (search Asset Store)  
   Multiple free options. Filter by: Free, 3D, Environment.

**Quaternius (quaternius.com) — CC0 Animals Pack:**
- Fully free, low-poly, optimised for mobile.
- Includes fish, turtle, dolphin.
- Download → import as FBX → create prefab → assign to FishSchool.

**Sketchfab (sketchfab.com):**
- Filter: Free + Downloadable + Low-poly
- Search "fish cartoon" or "low poly fish"
- Download as FBX or glTF. Most are Rigged.
- Import via Unity's FBX importer.

### How to Create a Fish Prefab

Once you have a fish FBX:

1. Import FBX into `Assets/Models/Fish/`
2. Select the model in Project window → Model tab:
   - Scale Factor: adjust until fish is ~0.3–0.5m long
   - Import Animation: ON (if model has swim cycle)
3. Drag model into scene, add Animator component
4. Create Animation Controller at `Assets/Animations/FishSwim.controller`
5. Add the swim cycle clip as default state (looping)
6. Drag prefab back to Project window → `Assets/Prefabs/Fish_01.prefab`
7. Assign to FishSchool → Fish Prefab field in Inspector

### Minimum Viable Asset Setup (1 Day of Work)

For your first functional test, you can use Unity primitives:

1. Create a Capsule GameObject, scale to (0.1, 0.05, 0.3)
2. Add a blue Material to it
3. Make it a prefab: `Assets/Prefabs/FishPlaceholder.prefab`
4. Assign to all three FishSchool prefab slots
5. This lets you test ALL logic (movement, profile switching, WebSocket) without any real art

Replace with real fish models during week 2.

---

## 10. Audio Assets

### Sources (royalty-free, suitable for clinical use)

**Freesound.org** (free with attribution or CC0):
- Search: `underwater ambient loop` → filter: CC0
- Search: `bubble underwater loop` → filter: CC0
- Search: `gentle piano loop` (for music layer)

**Pixabay.com** (fully free, no attribution):
- Search: `ocean underwater`, `gentle bubbles`

**ZapSplat.com** (free with account):
- High quality, well organised by category

### Import Settings (important for Quest performance)

For all audio clips in Unity:
```
Load Type: Compressed In Memory
Compression Format: Vorbis
Quality: 70
Sample Rate: Override → 22050 Hz
```

Background music: increase Quality to 85, keep Vorbis.

Avoid WAV on Quest — uncompressed audio chews RAM on a mobile chipset.

---

## 11. ADB Commands — Auto-Launch on Boot

These commands make the Quest open PediatricVR automatically when powered on, bypassing the Meta home shell entirely. This is the intended clinical deployment mode.

```bash
# === ENABLE AUTO-LAUNCH ===

# Step 1: Set PediatricVR as default home activity
adb shell cmd package set-home-activity \
  com.pediatricvr.distraction/com.unity3d.player.UnityPlayerGameActivity

# Step 2: Verify it was set
adb shell cmd package get-home-activity
# Expected: com.pediatricvr.distraction/com.unity3d.player.UnityPlayerGameActivity


# === DISABLE AUTO-LAUNCH (revert to Meta shell) ===

# Restore the Meta home app
adb shell cmd package set-home-activity \
  com.oculus.vrshell/.HomeActivity

# Or use the Unity Editor shortcut:
# PediatricVR → Set as Default Launcher (Auto-Boot)


# === OTHER USEFUL ADB COMMANDS ===

# Launch app without wearing headset (testing)
adb shell am start -n \
  com.pediatricvr.distraction/com.unity3d.player.UnityPlayerGameActivity

# Force-stop the app
adb shell am force-stop com.pediatricvr.distraction

# Live log output from Quest
adb logcat -s Unity:V

# Filter for PediatricVR-specific logs only
adb logcat | grep -E "\[WSS\]|\[GameManager\]|\[CommandProcessor\]|\[HTTP\]"

# Check if WebSocket port is listening (confirms server started)
adb shell ss -tlnp | grep 8080

# List installed packages (verify APK is installed)
adb shell pm list packages | grep pediatric

# Clear app data (full reset — useful between test sessions)
adb shell pm clear com.pediatricvr.distraction

# Screenshot from Quest to PC (for debugging idle screen layout)
adb exec-out screencap -p > quest_screenshot.png
```

---

## 12. Hospital WiFi Configuration

### Requirements to Confirm with Hospital IT

Before piloting, get written confirmation from the hospital IT/network team on each of these. Do not assume.

| Item | What You Need | Why |
|---|---|---|
| SSID | Dedicated SSID or existing staff WiFi | Quest and nurse phone must be on same subnet |
| Subnet isolation | Client isolation OFF | If enabled, devices on same WiFi cannot reach each other — WebSocket breaks |
| Port 8080 | Not blocked by firewall | WebSocket server port |
| Port 8081 | Not blocked | HTTP nurse panel port |
| DHCP reservation | Static IP for Quest MAC address | Quest IP must not change between power cycles |
| Guest network | Do NOT use guest WiFi | Guest networks almost always have client isolation ON |

### Getting the Quest MAC Address

```bash
adb shell ip link show wlan0
# Look for: link/ether XX:XX:XX:XX:XX:XX
```

Provide this MAC to IT for DHCP reservation. They assign the Quest a fixed IP (e.g., `192.168.10.50`). You then pre-configure the nurse panel with that IP permanently.

### If Hospital IT Won't Grant Static IP

Buy a travel router (TP-Link TL-WR902AC, ~€25). Create a private WiFi network just for the VR setup. Quest and nurse phone connect to it. No hospital IT involvement needed. Simpler for a pilot.

---

## 13. Nurse Panel Deployment

There are three ways to deliver the nurse panel to nursing staff. Choose based on hospital WiFi situation.

### Method A — Served from Quest (Default, Recommended)

The `SimpleHTTPServer.cs` script serves `nurse-panel.html` from the Quest on port 8081. The Quest auto-injects its own IP into the HTML at boot.

**Nurse workflow:**
1. Quest boots → PediatricVR loads → idle screen shows: `http://192.168.10.50:8081`
2. Nurse opens that URL on phone browser
3. Panel loads pre-connected. No IP entry needed.

This method works if the Quest has a static IP (see § 12).

### Method B — HTML File on Nurse's Phone

1. Copy `nurse-panel.html` to the nurse's phone (WhatsApp, email, AirDrop)
2. Open with Chrome/Safari → "Open as file"
3. Nurse manually enters Quest IP

**Problem:** Chrome on Android blocks `ws://` connections from `file://` origins (mixed content). Use **Chrome Flags** to bypass during dev, or use Firefox which is more permissive.

**For production:** Method A is cleaner. Method B is a backup.

### Method C — Hosted on Local Network

Host `nurse-panel.html` on any PC on the hospital network (Python HTTP server, nginx, even a Raspberry Pi):

```bash
# On any PC on hospital WiFi:
python3 -m http.server 9000 --directory path/to/WebUI/

# Nurse opens: http://[PC-IP]:9000/nurse-panel.html
# Then enters Quest IP manually
```

---

## 14. Testing Checklist

Complete in order. Do not skip to Quest testing without passing § 14.1.

### 14.1 Editor Testing (No Quest Required)

- [ ] Project compiles with 0 errors after all scripts copied in
- [ ] All Inspector references assigned (no null warnings on Play)
- [ ] Press Play → Console shows WebSocket server start message
- [ ] Open `nurse-panel.html` in Chrome on the same PC
- [ ] Enter `localhost` or PC IP, click Connect → status shows "Connected"
- [ ] Click Profile A, click START → Console shows "[GameManager] Session STARTED"
- [ ] Click STOP → Console shows "[GameManager] Session STOPPED"
- [ ] Session timer in nurse panel counts up, stops on STOP
- [ ] RESET button returns app to idle state

### 14.2 Quest Build Testing (Solo Developer)

- [ ] APK builds without errors (check Output window carefully for warnings)
- [ ] `adb install` succeeds
- [ ] App launches and shows idle screen with correct IP displayed
- [ ] Idle screen IP matches Quest's actual WiFi IP (`adb shell ip addr show wlan0`)
- [ ] WebSocket status dot on idle screen shows green "Server ready"
- [ ] Nurse panel served at port 8081 (open in phone browser)
- [ ] Connect from phone → Quest idle screen updates connection indicator
- [ ] Start session → fade-in occurs → placeholder fish (or real fish) visible
- [ ] Audio fades in correctly
- [ ] Stop from nurse panel → fade-out → return to idle screen
- [ ] Auto-timeout at 15 min works (test with reduced value in Inspector first)

### 14.3 Profile Testing

- [ ] Profile A: HeadOrientationInput is disabled, fish move slowly, music is quieter
- [ ] Profile B: HeadOrientationInput is enabled, GuidedCreature visible and responds to head yaw
- [ ] Switch profiles between sessions: start A, stop, start B — no leftover state

### 14.4 Resilience Testing

- [ ] Nurse panel WiFi drops mid-session: session continues (correct — do not auto-stop)
- [ ] Nurse panel reconnects after drop: commands work again
- [ ] Quest reboots: app auto-launches if HOME category is set, shows idle screen
- [ ] 15-minute auto-timeout: session ends gracefully without nurse input
- [ ] Multiple Start commands in quick succession: only first is processed (log shows warning for repeat)

---

## 15. Troubleshooting

### "WebSocket server not found" in console at runtime

- WebSocketSharp DLL not in `Assets/Plugins/WebSocketSharp/`
- DLL platform flags not set to Android ARM64
- `link.xml` missing or incorrectly formatted

### Nurse panel shows "Connection error" immediately

- Quest and phone are not on the same WiFi subnet
- Hospital WiFi has client isolation enabled (§ 12)
- Port 8080 blocked by hospital firewall
- Wrong IP entered — verify with: `adb shell ip addr show wlan0 | grep "inet "`

### Nurse panel served but HTML is blank / broken

- `nurse-panel.html` is not in `Assets/StreamingAssets/WebUI/`
- `Application.streamingAssetsPath` on Android points to inside the APK — verify with:
  ```csharp
  Debug.Log(Application.streamingAssetsPath);
  // Should be: jar:file:///data/app/...!/assets
  ```
  If StreamingAssets path is wrong, set the HTML content as a string in SimpleHTTPServer directly.

### Fish schools visible but not moving

- `SetActive(true)` not being called — check UnderwaterController is receiving StartExperience call
- FishPrefab not assigned on FishSchool components
- `_isActive` flag not set: verify `FishSchool.SetActive(true)` is called in UnderwaterController

### Build fails with "API Level" error

- Set both min and target SDK explicitly in Player Settings (§ 7)
- Install required Android SDK Platform 32 and 33 via Unity Hub → Android SDK Manager

### IL2CPP crash at runtime with "MissingMethodException"

- `link.xml` not in `Assets/Plugins/` — Unity must find it there, not in a subdirectory
- Add `ControlBehavior` and `NurseCommand` to link.xml → `Assembly-CSharp` block (already done)
- Try setting Managed Stripping Level to **Minimal** as a debug step

### App does not auto-launch on boot

- HOME/DEFAULT categories in AndroidManifest must survive Unity's manifest merge
- Verify with: `adb shell cmd package get-home-activity`
- If it still shows Meta shell: rerun `adb shell cmd package set-home-activity ...` after every new install

---

## 16. Script Execution Order

Set in: `Edit → Project Settings → Script Execution Order`

| Script | Order Value |
|---|---|
| WebSocketServerManager | -200 |
| GameManager | -100 |
| AgeProfileManager | -50 |
| CommandProcessor | 0 (default) |
| AppBootstrap | +50 |
| All others | default |

This ensures the network server is up before GameManager tries to log its address, and GameManager is alive before CommandProcessor tries to call it.

---

## Summary: First Week Plan

| Day | Task |
|---|---|
| Day 1 | Install SDK + WebSocketSharp, confirm project compiles, complete § 14.1 (Editor test) |
| Day 2 | Source fish assets (§ 9), create prefabs, build scene hierarchy (§ 5) |
| Day 3 | Wire all Inspector references (§ 6), first Quest build, idle screen visible |
| Day 4 | Nurse panel WebSocket test end-to-end, start/stop flow working |
| Day 5 | Audio integration, fade transitions, auto-launch ADB setup |
| Day 6–7 | Profile A/B switching, head orientation testing (Profile B), full checklist (§ 14) |

---

*Document version 1.0 — generated for PediatricVR development handoff*
