# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

iRacing Sequence Director is a WPF desktop application for creating replay edits in iRacing. Users create "nodes" (camera changes and frame skips) that are automatically applied during playback, enabling replay editing without external video software.

## Build Commands

```bash
# Restore NuGet packages
nuget restore ReplayTimeline.sln

# Build solution (Release)
msbuild ReplayTimeline.sln /p:Configuration=Release /p:Platform=AnyCPU

# Build solution (Debug)
msbuild ReplayTimeline.sln /p:Configuration=Debug /p:Platform=AnyCPU

# Output location
# ReplayTimeline/bin/Release/iRacingSequenceDirector.exe
```

**Prerequisites**: .NET Framework 4.7.2 SDK, Visual Studio 2015+

## External Dependencies

The project depends on iRacing SDK wrapper DLLs not included in this repository:
- `iRacingSdkWrapper.dll`, `iRacingSimulator.dll`, `iRSDKSharp.dll`
- Expected at: `../../iRacingSdkWrapper/iRacingSimulator/bin/Release/`
- These provide the iRacing telemetry and control interface

## Architecture

**MVVM Pattern** with WPF data binding:

- **View**: `View/MainWindow.xaml` - Single window with driver list, camera list, timeline, and playback controls
- **ViewModel**: `ViewModel/ReplayDirectorVM.cs` (main logic) + `ReplayDirectorVM.Properties.cs` (property definitions)
- **Model**: `Model/` - Node types, Driver, Camera, and capture mode abstractions
- **Commands**: `Commands/` - ~30 ICommand implementations organized by category (Menu Items, Nodes, Playback, Session)

**Key Components**:

| Component | Responsibility |
|-----------|---------------|
| `ReplayDirectorVM` | SDK connection, telemetry handling, node application during playback, project persistence |
| `Node` (abstract) | Base class for timeline entries with frame number and enabled state |
| `CamChangeNode` | Switch driver/camera at specified frame |
| `FrameSkipNode` | Jump to next node's frame during playback |
| `NodeCollection` | Manages ordered list of nodes, auto-assigns skip targets |
| `CaptureModeBase` | Strategy pattern for recording (None, iRacing, OBS, ShadowPlay) |
| `Sim.Instance` | Static singleton providing SDK access throughout application |

**Data Flow**:
1. SDK wrapper polls iRacing telemetry (frame position, session info, driver data)
2. ViewModel receives updates via events and updates bound properties
3. During playback at 1x speed, nodes are applied when their frame is reached
4. Projects are auto-saved/loaded per SessionID as JSON files

## Key Patterns

- **Observer**: INotifyPropertyChanged for reactive UI
- **Command**: All user actions via ICommand
- **Strategy**: Capture modes implement `CaptureModeBase`
- **Value Converters**: `ViewModel/Converters/` for WPF binding transformations

## Auto Director Feature

Automatic camera control that generates camera plans for replays. Works in two modes:
- **Event-Driven Mode (Default)**: No LLM required. Camera switches triggered by detected events with anticipation timing.
- **LLM Mode (Optional)**: Uses OpenAI or local models for AI-powered camera selection.

**Architecture** (`AI/` folder):
- `AI/Models/` - RaceEvent, TelemetrySnapshot, CameraPlan data models
- `AI/EventDetection/` - Detectors for incidents, overtakes, and battles
- `AI/LLM/` - Provider abstraction supporting OpenAI API and local models (Ollama/LM Studio)
- `AI/Director/` - AIDirector orchestrator and settings

**Workflow**:
1. **Scan Replay**: Jump through frames collecting telemetry snapshots
2. **Detect Events**: Run detectors to find incidents, overtakes, battles
3. **Generate Plan**: Event-driven mode creates plan from events; LLM mode sends to model for JSON response
4. **Apply Plan**: Convert plan to CamChangeNodes in NodeCollection

**LLM Integration** (optional):
- `ILLMProvider` interface with `OpenAIProvider` and `LocalModelProvider`
- Uses OpenAI-compatible chat completions API format
- Prompts in `PromptTemplates.cs` instruct LLM to output JSON camera plan

**Commands**: `Commands/AI/` - ScanReplayCommand, GenerateCameraPlanCommand, ApplyAIPlanCommand

**Settings**: Configure via Auto Director > Settings menu (detection options, anticipation timing, optional LLM)

## No Test Suite

Testing is manual against live iRacing sessions. No unit test framework is configured.
