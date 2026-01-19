# iRacing Sequence Director

**_I no longer have an active iRacing subscription so I've made the source code available for anyone interesting in updating this_**

Create your own replay edits without any required editing knowledge. Create 'nodes' at points in your replay and choose this point to either cut to a different driver and/or camera or skip to further ahead in the replay. Sequence Director will then play this sequence of shots and time-skips back for recording or viewing.

- No need for additional editing knowledge or software
- Useful for anyone using editing software for testing cuts in iRacing and recording sequences of shots without recording multiple clips
- Support for recording using iRacing's built-in capture facility (no need for any external software)
- Support for capturing with overlays for race standings, etc. when using OBS Studio
- Each replay has its own save file so no need to edit everything in one session
- Works with custom cameras for setting up your own shots with the iRacing camera tool
- Resizeable window for users with limited screen space
- **AI Director** - Automatically generate camera plans using LLM-powered analysis

![App screenshot](https://i.ibb.co/6nGVxfh/Main.png)

## AI Director

The AI Director feature automatically analyzes your replay and generates professional-style camera sequences using AI/LLM technology.

### Quick Start

1. Open a replay in iRacing
2. Launch Sequence Director
3. Go to **AI Director > Scan Replay** (set your frame range)
4. Click **AI Director > Generate Camera Plan**
5. Click **AI Director > Apply AI Plan**
6. Play or record your sequence!

### How It Works

#### Step 1: Scan Replay
The AI Director scans through your replay detecting key events:
- **Incidents** - Off-track excursions, spins, contact
- **Battles** - Close racing between drivers (gap < 2% of lap)
- **Overtakes** - Position changes between drivers

#### Step 2: Generate Camera Plan
Sends the detected events to an LLM (OpenAI or local model) which creates a broadcast-style camera sequence with:
- Varied camera angles (TV, Chase, Cockpit, Helicopter, etc.)
- Professional pacing (8-15 second cuts)
- Coverage of key moments

#### Step 3: Apply Plan
The AI selects which driver to follow for each camera switch using the Driver Selection Algorithm, then adds nodes to your list.

**Tip:** You can re-apply the plan with different driver selection settings without rescanning or regenerating!

---

### AI Director Settings

Access via **AI Director > Settings** menu. Three tabs are available:

#### LLM Provider Tab

Configure your AI model:

**OpenAI (Recommended)**
- Enter your OpenAI API key
- Select model: `gpt-4o` (best quality), `gpt-4o-mini` (faster/cheaper), `gpt-3.5-turbo`
- Click "Test Connection" to verify

**Local Models**
- Works with Ollama, LM Studio, or any OpenAI-compatible API
- Default endpoint: `http://localhost:11434/v1/chat/completions`
- Enter your model name (e.g., `llama3`, `mistral`)

#### Event Detection Tab

Control what events are detected during scanning:

| Setting | Default | Description |
|---------|---------|-------------|
| Detect Incidents | ✓ | Off-track, spins, contact |
| Detect Overtakes | ✓ | Position changes |
| Detect Battles | ✓ | Close racing (cars within gap threshold) |
| Scan Interval | 60 frames | Lower = more detail but slower scan |

#### Driver Selection Tab

**This is where you tune how drivers are chosen for each camera cut.**

##### Event Weights (0-100)
How much each event type influences driver selection:

| Setting | Default | Description |
|---------|---------|-------------|
| Incidents | 50 | Crashes, spins, off-tracks |
| Overtakes | 40 | Position changes |
| Battles | 35 | Close racing |

##### Bonus Weights
Additional factors that boost driver priority:

| Setting | Default | Description |
|---------|---------|-------------|
| Momentum | 25 | Drivers gaining multiple positions ("on a charge") |
| Pack Racing | 15 | Drivers in groups of 3+ cars |
| Fresh Action | 15 | Recent position changes (decays over 20 sec) |
| Position | 15 | Baseline interest from race position |

##### Variety Control
**These are the most important settings for balancing action vs. variety:**

| Setting | Default | Description |
|---------|---------|-------------|
| Variety Strength | 60 | Penalty for recently-shown drivers (higher = more switching) |
| Action Override | 40% | How much exciting action reduces variety penalty |
| Min Cuts/Minute | 4 | Minimum driver switches per minute |

---

### Tuning Guide

**Problem: Same few drivers shown repeatedly**
- Increase **Variety Strength** (try 70-80)
- Decrease **Action Override** (try 20-30%)
- The variety penalty will apply more strongly

**Problem: Missing exciting action**
- Increase **Event Weights** (Incidents, Overtakes, Battles)
- Increase **Action Override** (try 50-60%)
- Action will override variety penalty more

**Problem: Too much focus on race leader**
- Decrease **Position** weight (try 5-10)
- Increase **Pack Racing** weight (shows mid-pack action)

**Problem: Cuts feel random/unfocused**
- Increase **Event Weights** to prioritize action
- Decrease **Min Cuts/Minute** for longer shots

**Recommended starting points:**

| Style | Variety | Action Override | Event Weights |
|-------|---------|-----------------|---------------|
| Broadcast (balanced) | 60 | 40% | 50/40/35 |
| Action-focused | 40 | 60% | 60/50/45 |
| Maximum variety | 80 | 20% | 40/35/30 |

---

### How Driver Selection Works

When applying a camera plan, the AI scores each driver at each camera switch point:

```
Final Score = Action Score + Position Score - Variety Penalty - Overexposure Penalty + Bonuses
```

**Action Score** = Events + Momentum + Battle + Pack + Fresh Action

**Variety Dampening**: When a driver has high action, their variety penalty is reduced (but never below 40% of the base penalty). This ensures exciting moments are shown while still maintaining variety.

**The driver with the highest final score is selected for each camera cut.**

---

### Tips & Best Practices

1. **Start with defaults** - They're tuned for balanced broadcast-style coverage

2. **Re-apply to test settings** - After changing driver selection weights, just click "Apply AI Plan" again (no rescan needed)

3. **Longer replays** - The system automatically chunks replays >10 minutes into segments for better LLM handling

4. **Local models** - For privacy or cost savings, use Ollama with `llama3` or similar. Quality may vary.

5. **Camera variety** - The LLM handles camera selection; driver selection settings don't affect camera angles

## Installation

- Download the latest version from the [Releases](https://github.com/GetUpKidAK/iRacingSequenceDirector/releases) page. 
- Extract to your preferred location and run iRacingSequenceDirector.exe with a replay open

## How to use
### Camera Changes
A Camera Change node will cut to the selected car and camera combination when the desired frame is reached
- Use the playback controls to get to the desired time, select a driver and camera, and click 'Add Cam Change'.

![Add Cam Change](https://i.ibb.co/6FJ9KNk/Add-Cam-Change1.png)

This adds the node to the list for playback:

![Node list](https://i.ibb.co/KrNq37Z/Add-Cam-Change2.png)

And that's pretty much it! Repeat the process, adding nodes where desired. You can edit or delete existing nodes by selecting them and picking a new car/camera combination, or pressing 'Delete Node'.

### Frame Skips
A Frame Skip node will, when the desired frame is reached, jump ahead to the next node in the list.
- Use the playback controls to get to the point in the replay where you want to skip _from_ and click 'Add Frame Skip'.

![Add Frame Skip](https://i.ibb.co/HCrcZKp/Add-Frame-Skip-P1.png)

This will add a disabled Frame Skip node to the list.

![Node list](https://i.ibb.co/9qXY14K/Add-Frame-Skip-P2.png)

- Now you can use the playback controls to find the point you want to skip _to_ and select your desired car/camera combination. Add a Camera Change node as above.

![Frame Skip Added](https://i.ibb.co/VptvZkM/Add-Frame-Skip-P3.png)

Now the Frame Skip node will update to show which frame it will skip to.
_In this example, once the replay reaches frame #49942 it will jump ahead to frame #58772 and cut to the selected camera._

**Note:** If you're making long jumps in the replay and it takes too long or doesn't skip far enough, please check the guides below.

Once you've finished 'editing' rewind back to where you'd like the replay to start and press Play to view what you've created, or Record to capture it using the in-sim capture or OBS. Guides for setting that up are below...

### Video
Here's a video explaining some of the basics in an early version (pre-frame skipping). I'll try and do a video explaining frame skips soon:

[![How to Use](https://i.ibb.co/Lpp6wTr/Thumbnail-Play.png)](https://www.youtube.com/watch?v=amghnO6rE7U)

## Guides/More Info

### Using Frame Skip Nodes

iRacing has a 'replay spooling' option which can cause issues with skipping over certain lengths of the replay. If this is enabled you may get long delays when skipping and/or it may stop before it gets to the point you want to skip to. You can stop these issues by disabling this setting under the 'Replay' section of the in-sim options:

![Spooling option](https://i.ibb.co/44nTQbf/Spooling1.png)

You need to restart the sim after changing this setting. When disabled this can also limit the size of the replay when the sim is in a live session so make sure you only disable this when editing if you run longer races.

### Setting up iRacing in-sim capture

- To use the in-sim recording the 'Enable video and screen capture' setting must be enabled in iRacing.
You'll need to restart the sim if this wasn't already enabled.

![Misc Settings](https://i.ibb.co/Rppt27d/Misc-Settings.jpg)

- Select the 'In-Sim Capture' setting in Sequence Director under the Capture Mode drop-down:

![Capture settings](https://i.ibb.co/FBJGtrK/image.png)

**Notes:**
The videos are saved in My Documents/iRacing/videos.

You can adjust the output quality and file format of the recordings in the app.ini file. The default is 720p30fps.
This is found under My Documents/iRacing. The lines are commented but look like this:

![app.ini](https://i.ibb.co/92cbP7M/appIni.png)

### Setting up OBS recording

To use OBS recording without manually starting the recording process, you'll need to configure a hotkey for it.

- Via the Settings -> Hotkeys menu in OBS set 'Ctrl+Shift+R' as the hotkey for both Start and Stop Recording:

![Hotkeys](https://i.ibb.co/89hcks2/image.png)

- Select the 'OBS Studio' setting in Sequence Director under the Options -> Recording Capure Method menu:

![Capture settings](https://i.ibb.co/FBJGtrK/image.png)

**Note:** Only [OBS Studio](https://obsproject.com/) is supported at this time. The hotkey required can't be changed at this time.

### Using custom cameras

You can use your own modified cameras using the iRacing Camera Tool. I've written a basic guide on how to do that here: https://docs.google.com/document/d/1EOWFVIqH9OppcqurmR_wzZs_czvqj3_zsKU_qR6fKfo/edit?usp=sharing

## Support

You can ask any questions or get support for the app over at the [iRacing Forums](https://forums.iracing.com/discussion/605/iracing-sequence-director-editing-tool-for-replays).

If you *really* like the app, you can make a donation via [PayPal](https://paypal.me/GetUpKidAK?locale.x=en_GB). Thanks!
