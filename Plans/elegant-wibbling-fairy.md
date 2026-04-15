# Topology Map Feature — SE Command Center (se-lz)

> **STATUS: NOT STARTED** | This plan is for the **SECC repo** (se-lz), not veeam-healthcheck. All implementation targets `/src/SECC.Web/` and `/src/SECC.Infrastructure/`. Zero code written as of 2026-03-27.

## Context

The SE Command Center already parses VHC HTML reports into structured models (proxies, repos, SOBRs, jobs, managed servers, etc.) stored in SQLite via EF Core. The goal is to add an interactive topology visualization that shows a customer's Veeam infrastructure as a graph — nodes for each component, edges for relationships — so SEs can instantly understand environment architecture at a glance.

**Visualization library:** Cytoscape.js (hierarchical layout, zoom/pan/drag, export to PNG).

## Architecture

The topology will be:
1. A **new Blazor page** at `/vhc/report/{id}/topology` (also accessible as a tab from ReportView)
2. A **C# service** that transforms the existing Report model into a graph structure (nodes + edges)
3. A **JS interop layer** that passes the graph JSON to Cytoscape.js for rendering
4. **No new database models needed** — all data already exists in the parsed Report entity

## Implementation Plan

### Step 1: Add Cytoscape.js to the project

**Files:**
- `/src/SECC.Web/wwwroot/lib/cytoscape/cytoscape.min.js` — download from CDN or npm
- `/src/SECC.Web/wwwroot/js/topology.js` — JS interop module for initializing and controlling the graph
- `/src/SECC.Web/Components/App.razor` — add `<script>` references

**`topology.js` responsibilities:**
- `window.topologyInterop.init(elementId, graphJson, layoutOptions)` — initialize Cytoscape instance
- `window.topologyInterop.exportPng()` — export current view as PNG
- `window.topologyInterop.fitView()` — reset zoom to fit all nodes
- `window.topologyInterop.setLayout(layoutName)` — switch between hierarchical/circular/force layouts
- `window.topologyInterop.destroy()` — cleanup on page dispose

### Step 2: Create the Topology Graph Builder service

**File:** `/src/SECC.Infrastructure/Services/TopologyGraphBuilder.cs`

This service transforms a `Report` entity into a graph JSON structure. No new interface needed — it's a pure transformation utility.

```csharp
public class TopologyGraphBuilder
{
    public TopologyGraph BuildGraph(Report report);
}
```

**Node types & sources:**

| Node Type | Source Model | Color | Icon |
|-----------|-------------|-------|------|
| VBR Server | `BackupServer` | Blue (#1976D2) | Server |
| SQL Server | `BackupServer` (name parsing) | Indigo (#303F9F) | Database |
| Proxy | `ProxyInfo` | Green (#388E3C) | Hub |
| Repository | `RepositoryInfo` | Orange (#F57C00) | Storage |
| SOBR | `SobrInfo` | Purple (#7B1FA2) | Cloud |
| SOBR Extent | `SobrExtent` | Light Purple (#9C27B0) | Folder |
| Managed Server (Hypervisor) | `ManagedServer` where Type contains "Vi" or "Hv" | Teal (#00796B) | Monitor |
| WAN Accelerator | `BackupServer.WanAccelerator == true` | Brown (#5D4037) | Network |

**Edge types & derivation:**

| Edge | How to derive | Label |
|------|--------------|-------|
| VBR → SQL Server | Both from `BackupServer` model | "Database" |
| VBR → Proxy | `ProxyInfo` items exist → connect to VBR | "Manages" |
| VBR → Repository | `RepositoryInfo` items exist → connect to VBR | "Manages" |
| VBR → Managed Server | `ManagedServer` items → connect to VBR | "Manages" |
| Job → Repository | `JobDetail.Repository` matches `RepositoryInfo.Name` or `SobrInfo.Name` | "Targets" |
| SOBR → SOBR Extent | `SobrExtent.SobrName` matches `SobrInfo.Name` | "Contains" |
| SOBR → Capacity Tier | `SobrInfo.CapacityTierEnabled == true` → virtual cap tier node | "Copy/Move" |
| SOBR → Archive Tier | `SobrInfo.ArchiveTierEnabled == true` → virtual archive tier node | "Archive" |
| Repository → Gateway | `RepositoryInfo.SpecifiedGateways` not null → parse server names | "Gateway" |
| Proxy on VBR host | `BackupServer.ProxyRole == true` → merge proxy node onto VBR node | (role badge) |
| Repo on VBR host | `BackupServer.RepoRole == true` → merge repo node onto VBR node | (role badge) |

**Multi-role handling:** When `BackupServer.ProxyRole` or `BackupServer.RepoRole` is true, annotate the VBR Server node with role badges rather than creating duplicate nodes. The `ServerRequirement.Type` field (e.g., "Proxy / Repository / BackupServer") can be used to validate.

**Graph model classes:**

```csharp
public record TopologyGraph(List<TopologyNode> Nodes, List<TopologyEdge> Edges);
public record TopologyNode(string Id, string Label, string NodeType, string? ParentGroup, Dictionary<string, string> Metadata);
public record TopologyEdge(string Source, string Target, string EdgeType, string? Label);
```

### Step 3: Create the Topology Blazor page

**File:** `/src/SECC.Web/Components/Pages/Topology.razor`

- Route: `@page "/vhc/report/{Id:int}/topology"`
- Injects: `IReportRepository`, `IJSRuntime`
- On load: fetch full report with all includes, call `TopologyGraphBuilder.BuildGraph()`, serialize to JSON, invoke `topologyInterop.init()` via JS interop
- Toolbar: layout switcher (Hierarchical/Circular/Force), Export PNG button, Fit View button, legend toggle
- Legend: color-coded node type key
- Full-page height canvas with MudBlazor paper container

### Step 4: Add navigation entry

**File:** `/src/SECC.Web/Components/Layout/NavMenu.razor`
- Add a "Topology" nav link inside the "VHC Analysis" group (but it's per-report, so more likely...)

**File:** `/src/SECC.Web/Components/Pages/ReportView.razor`
- Add a "View Topology" button in the header toolbar next to "Ask AI About This Report"
- Routes to `/vhc/report/{Id}/topology`

### Step 5: Style the graph

**File:** `/src/SECC.Web/wwwroot/app.css`
- Add topology container styles (full height, dark/light theme compatibility)
- Node label styling via Cytoscape stylesheet (passed in JS)

## Key Design Decisions

1. **No new database models** — all topology data is derived from existing parsed models at render time
2. **Client-side rendering** — Cytoscape.js runs in the browser; the server just provides the graph JSON
3. **JS interop pattern** — standard Blazor `IJSRuntime.InvokeVoidAsync` calls to topology.js module
4. **Hierarchical layout default** — VBR at root, infra components in tiers, most readable for Veeam topologies
5. **Node grouping for multi-role hosts** — rather than separate nodes for every role, hosts with multiple roles get compound nodes with role badges

## Files to Create/Modify

| File | Action |
|------|--------|
| `src/SECC.Web/wwwroot/lib/cytoscape/cytoscape.min.js` | Create (download library) |
| `src/SECC.Web/wwwroot/js/topology.js` | Create (JS interop module) |
| `src/SECC.Infrastructure/Services/TopologyGraphBuilder.cs` | Create (graph transformation) |
| `src/SECC.Web/Components/Pages/Topology.razor` | Create (Blazor page) |
| `src/SECC.Web/Components/App.razor` | Modify (add script ref) |
| `src/SECC.Web/Components/Pages/ReportView.razor` | Modify (add topology button) |
| `src/SECC.Web/wwwroot/app.css` | Modify (add topology styles) |

## Verification

1. **Build:** `dotnet build src/SECC.Web/SECC.Web.csproj` — compiles without errors
2. **Runtime:** Upload a VHC report → navigate to report → click "View Topology" → graph renders with correct nodes
3. **Node validation:** Verify VBR server, proxies, repos, SOBRs, SOBR extents all appear as distinct nodes
4. **Edge validation:** Verify job→repo, SOBR→extent, VBR→proxy edges render correctly
5. **Layout:** Hierarchical layout produces readable output; switching layouts works
6. **Export:** PNG export produces a valid image
7. **Multi-role:** A server with ProxyRole=true and RepoRole=true shows as single node with both role badges
