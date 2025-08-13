# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 6000.0.42f1 project called "ParticleTest" that uses the Universal Render Pipeline (URP). The project includes Unity MCP (UMCP) integration for enhanced Unity Editor control and automation.

## Common Development Commands

### Unity MCP Integration
The project has Unity MCP integration which provides direct Unity Editor control. Key MCP tools available:
- `mcp__umcp-docker__GetUnityClientState` - Check if Unity is running and its current state
- `mcp__umcp-docker__ManageScene` - Create, load, save scenes
- `mcp__umcp-docker__RunTests` - Run Unity tests
- `mcp__umcp-docker__GetTests` - List available tests
- `mcp__umcp-docker__ReadConsole` - Read Unity console logs
- `mcp__umcp-docker__ForceUpdateEditor` - Force Unity to update/refresh
- `mcp__umcp-docker__ExecuteMenuItem` - Execute Unity menu items

### Testing
The project uses Unity Test Framework with tests in the UMCP.Tests.Editor assembly:
```bash
# Run all tests via MCP
mcp__umcp-docker__RunTests --TestMode All

# Run specific test mode
mcp__umcp-docker__RunTests --TestMode EditMode
mcp__umcp-docker__RunTests --TestMode PlayMode

# List all tests
mcp__umcp-docker__GetTests --TestMode All
```

### Building
Unity projects are built through the Unity Editor, not command line. Use:
- File → Build Settings in Unity Editor
- Or use Unity's command line arguments for automated builds

## High-Level Architecture

### Project Structure
- **Assets/** - Unity project assets
  - **Scenes/** - Unity scenes (currently has SampleScene)
  - **Settings/** - Render pipeline and post-processing settings
  - **TutorialInfo/** - Tutorial assets and readme system
- **Packages/** - Unity Package Manager configuration
- **ProjectSettings/** - Unity project configuration files
- **Library/** - Unity's generated files (not version controlled)
- **UMCP Integration** - Unity MCP package for editor automation

### Key Technologies
- **Unity 6000.0.42f1** - Latest Unity 6 version
- **Universal Render Pipeline (URP)** - Modern scriptable render pipeline
- **Unity Input System** - New input handling system
- **Unity Test Framework** - Built-in testing framework
- **UMCP** - Unity MCP for editor automation and testing

### Assembly Structure
- **Assembly-CSharp** - Main game code assembly
- **Assembly-CSharp-Editor** - Editor-only code assembly
- **UMCP.Editor** - Unity MCP editor integration
- **UMCP.Tests.Editor** - Unity MCP test suite

### Important Notes
- Unity projects require the Unity Editor for most operations
- The project uses .meta files for asset tracking - never delete these
- Build outputs are platform-specific and configured in Build Settings
- Test results are stored in the TestResults/ directory
- Unity console logs can be accessed via MCP tools for debugging