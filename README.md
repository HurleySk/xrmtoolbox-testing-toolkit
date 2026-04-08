# XrmToolBox Testing Toolkit

A standalone test harness for [XrmToolBox](https://www.xrmtoolbox.com/) plugins. Load any plugin DLL, inject mock Dataverse responses, and automate UI testing -- no XrmToolBox installation required.

## Features

- **Dynamic plugin loading** -- loads any XrmToolBox plugin DLL via MEF (same mechanism XrmToolBox uses)
- **Configurable mock service** -- JSON-driven `IOrganizationService` mock with request matching and response configuration
- **SDK call recording** -- records every Dataverse SDK call for post-test verification
- **UI Automation ready** -- WinForms controls are discoverable via Microsoft UI Automation / FlaUI
- **Screenshot capture** -- built-in screenshot support (F12 key or programmatic)
- **CLI-driven** -- fully controllable via command line arguments

## Quick Start

### Build

```bash
dotnet build --configuration Release
```

### Run

```bash
XrmToolBox.TestHarness.exe --plugin "path\to\YourPlugin.dll" --mockdata "samples\basic-mockdata.json"
```

### CLI Options

| Flag | Description | Default |
|------|-------------|---------|
| `--plugin, -p <path>` | Path to plugin DLL (required) | |
| `--mockdata, -m <path>` | Path to JSON mock data config | (empty responses) |
| `--width <px>` | Window width | 1024 |
| `--height <px>` | Window height | 768 |
| `--screenshots, -s <dir>` | Screenshot output directory | |
| `--org <name>` | Organization display name | Mock Organization |
| `--record, -r <path>` | Record SDK calls to JSON on exit | |
| `--no-autoconnect` | Don't inject mock service on load | |

## Mock Data Configuration

Create a JSON file to define how the mock service responds to SDK calls:

```json
{
  "settings": {
    "throwIfUnmatched": false,
    "defaultDelay": 0
  },
  "responses": [
    {
      "operation": "RetrieveMultiple",
      "description": "Return accounts",
      "match": { "entityName": "account" },
      "response": {
        "entities": [
          {
            "logicalName": "account",
            "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "attributes": {
              "name": "Contoso Ltd"
            }
          }
        ],
        "moreRecords": false
      }
    }
  ]
}
```

### Match Criteria

| Key | Description |
|-----|-------------|
| `entityName` | Match by entity logical name |
| `requestType` | Match Execute requests by full type name |
| `queryExpressionEntity` | Match QueryExpression by entity name |
| `fetchXmlContains` | Match FetchExpression containing a substring |
| `*` | Wildcard -- matches anything |

Responses are matched in order; first match wins.

### Response Features

- **`resultsFile`** -- reference large payloads from separate JSON files
- **`delay`** -- simulate network latency (milliseconds)
- **`fault`** -- throw `FaultException<OrganizationServiceFault>` with custom error code and message

## UI Automation with FlaUI

XrmToolBox plugins use standard WinForms controls with Hungarian naming conventions (`btnLoad`, `dgvAttributes`, `cboSolutions`, `txtFilter`). WinForms automatically exposes the control `Name` as `AutomationId` in Microsoft UI Automation, making plugins immediately automatable with [FlaUI](https://github.com/FlaUI/FlaUI).

### With FlaUI-MCP (for AI agents)

[FlaUI-MCP](https://github.com/shanselman/FlaUI-MCP) exposes Windows UI Automation through the Model Context Protocol, letting AI assistants like Claude Code interact with the plugin UI.

**Quick setup** (requires .NET 8+ SDK):

```powershell
.\setup-flaui-mcp.ps1
```

This clones FlaUI-MCP, builds it, publishes to `C:\tools\FlaUI-MCP`, and registers it with Claude Code as the `flaui-mcp` MCP server. Options:

```powershell
.\setup-flaui-mcp.ps1 -InstallDir "D:\my-tools\FlaUI-MCP" -Scope project
```

**Manual setup:**

1. Clone and build FlaUI-MCP: `git clone https://github.com/shanselman/FlaUI-MCP && dotnet publish src/FlaUI.Mcp -c Release -o C:\tools\FlaUI-MCP`
2. Register with Claude Code: `claude mcp add flaui-mcp "C:\tools\FlaUI-MCP\FlaUI.Mcp.exe"`
3. Start the test harness with your plugin
4. Claude Code can now find controls, click buttons, read text, and take screenshots

## Architecture

```
XrmToolBox.TestHarness.sln
  src/
    XrmToolBox.TestHarness/           # WinForms host application
      Program.cs                      # Entry point with CLI parsing
      HarnessForm.cs                  # Form that hosts the plugin control
      PluginLoader.cs                 # MEF + reflection plugin discovery
      CommandLineOptions.cs           # CLI argument model

    XrmToolBox.TestHarness.MockService/  # Reusable mock library
      MockOrganizationService.cs      # IOrganizationService implementation
      MockDataStore.cs                # JSON mock data loading
      RequestMatcher.cs               # Request-to-response matching
      RequestRecorder.cs              # SDK call recording
```

## How It Works

1. **Plugin loading**: Uses MEF (`AssemblyCatalog` + `CompositionContainer`) to discover `IXrmToolBoxPlugin` exports, then calls `GetControl()` to get the `PluginControlBase` instance. Falls back to reflection if MEF fails.

2. **Service injection**: Calls `PluginControlBase.UpdateConnection()` with the mock service and a stub `ConnectionDetail`, exactly matching how XrmToolBox itself connects plugins.

3. **Connection handling**: Wires the `OnRequestConnection` event so that `ExecuteMethod()` calls (which check for an active connection) work correctly.

4. **Notification support**: Creates a `NotifPanel` control in the form, satisfying `PluginControlBase.ShowInfoNotification()` which searches for this by name.

## Requirements

- .NET Framework 4.8
- Windows (WinForms)

## License

MIT
