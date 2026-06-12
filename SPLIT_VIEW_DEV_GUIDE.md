## Split View Development Guide

### Quick Architecture Overview

```
BrowserWindow (UI Layer)
├── BrowserTabs TabControl (normal mode - visible)
├── SplitViewContainer (split mode - hidden until activated)
│   └── BuildPanelGridUI() recursively creates:
│       └── Grid (root)
│           ├── Grid (left/top panel)
│           │   └── TabControl (panels with tabs)
│           ├── GridSplitter (resizable divider)
│           └── Grid (right/bottom panel)
│               └── TabControl
│
├── SplitViewManager (State Management)
│   └── SplitViewState (Tree Structure)
│
└── SplitViewUIManager (Integration)
    ├── Panel registration
    ├── Tab distribution
    └── Active panel tracking
```

### Key Methods & Their Purposes

| Method | Purpose | Called From |
|--------|---------|-------------|
| `CreateSplitView(orientation)` | Initiates split, hides tabs, shows container | Button click / Ctrl+Shift+V/H |
| `BuildPanelGridUI(state)` | Recursively builds grid UI from state tree | `CreateSplitView()`, `ExitSplitView()` |
| `TransferTabsToPanel(panelId)` | Moves tabs from main TabControl to panel | `CreateSplitView()` |
| `ExitSplitView()` | Restores normal single-pane mode | Esc key / UI event |
| `LoadSplitViewState()` | Restores split config from settings | `InitializeTabs()` |
| `SaveSplitViewState()` | Persists state to settings | `SaveSession()` |
| `Window_PreviewKeyDown()` | Global keyboard handler | Window PreviewKeyDown |

### Data Flow Diagram

```
User Action (Button/Keyboard)
    ↓
CreateSplitView() / ExitSplitView()
    ↓
SplitViewManager.CreateSplit() / ClosePanel()
    ↓
BuildPanelGridUI() - Render new UI
    ↓
TransferTabsToPanel() - Distribute tabs
    ↓
Update Visibility (BrowserTabs ↔ SplitViewContainer)
    ↓
SaveSplitViewState() on window close
    ↓
Settings file (persistent storage)
    ↓
LoadSplitViewState() on window open
    ↓
Restore to previous state
```

### Adding New Features

#### 1. **Add Close Panel Button per Panel**
```csharp
// In BuildPanelGridUI(), for leaf panels:
var closeButton = new Button { Content = "×" };
closeButton.Click += (s, e) => SplitViewContainer.ClosePanel(panelId);
// Add to panel header
```

#### 2. **Add Drag & Drop Tab Support Between Panels**
```csharp
// In TransferTabsToPanel():
tabItem.AllowDrop = true;
tabItem.Drop += (s, e) => 
{
    var sourcePanel = GetPanelFromTab(e.Data);
    var targetPanel = panelId;
    _splitViewUIManager?.TransferTabBetweenPanels(sourcePanel, targetPanel, tabState);
};
```

#### 3. **Add Maximum Split Depth Limit**
```csharp
// In CreateSplitView():
int depth = GetSplitDepth(_currentActivePanelId);
if (depth >= 3) // max 3 levels deep
{
    CustomMessageBox.Show("Maximum split depth reached", "Limit");
    return;
}
```

#### 4. **Save Individual Panel URLs**
```csharp
// Modify SplitViewState to track panel URLs during navigation
// In CoreWebView2_NavigationCompleted:
var panelId = GetCurrentPanelId();
_splitViewManager.GetAllLeafPanels()
    .FirstOrDefault(p => p.GroupId == panelId)
    ?.TabUrl = newUrl;
```

### State Serialization Format

```json
{
  "GroupId": "uuid-1234",
  "Orientation": "Vertical",
  "SplitRatio": 0.5,
  "LeftTopPanel": {
    "GroupId": "uuid-5678",
    "Orientation": "Vertical",
    "TabUrl": "https://example.com",
    "TabUrls": ["url1", "url2"],
    "SelectedTabIndex": 0,
    "IsLeafPanel": true
  },
  "RightBottomPanel": {
    "GroupId": "uuid-9012",
    "IsLeafPanel": true,
    "TabUrl": "https://google.com"
  }
}
```

### Testing Checklist

- [ ] Vertical split creates 2 left/right panels
- [ ] Horizontal split creates 2 top/bottom panels
- [ ] Tabs visible in both panels
- [ ] Divider dragging resizes panels
- [ ] New tab prompts for target panel
- [ ] Keyboard shortcuts work (V/H/Esc)
- [ ] Split state persists across app restart
- [ ] Exit split view returns to normal mode
- [ ] Panel IDs are unique (GUID)
- [ ] Memory usage doesn't increase unexpectedly
- [ ] No WebView2 instances are duplicated
- [ ] Events are properly unsubscribed on cleanup

### Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| Tabs not visible in panels | TabControl Items not populated | Check `TransferTabsToPanel()` adds TabItems |
| Divider not draggable | GridSplitter styling issue | Verify GridSplitter.Cursor = SizeWE/NS |
| Split view doesn't restore | Serialization failed | Check JSON format, add null checks |
| Memory leak after closing panel | Event handlers not unsubscribed | Verify cleanup in `ExitSplitView()` |
| UI doesn't update after split | BuildPanelGridUI() not called | Ensure SplitViewContainer.Children cleared first |

### Performance Considerations

- `BuildPanelGridUI()` is O(n) where n = number of nested splits
- Typical usage: 1-3 levels of nesting
- Each panel creates one TabControl + one or more WebView2 instances
- Memory: ~50-100MB per WebView2 instance (normal browser overhead)

### Extension Points

1. **Custom Panel Styling** - Override `ApplyTabControlStyle()`
2. **Panel Context Menu** - Add right-click handling
3. **Persistence Strategy** - Modify `SaveSplitViewState()` to use different storage
4. **Keyboard Shortcuts** - Add more in `Window_PreviewKeyDown()`
5. **Panel Layouts** - Extend `SplitOrientation` enum for thirds, quarters, etc.

### Debugging Tips

```csharp
// Log split state structure
private void LogSplitState()
{
    var state = _splitViewManager.GetRootState();
    Debug.WriteLine($"Panels: {_splitViewManager.GetPanelCount()}");
    Debug.WriteLine($"IsInSplitMode: {_splitViewManager.IsInSplitMode}");
    foreach (var panel in _splitViewManager.GetAllLeafPanels())
    {
        Debug.WriteLine($"  Panel {panel.GroupId}: {panel.TabUrls.Count} tabs");
    }
}

// Verify tab distribution
foreach (var panelId in _splitViewUIManager.GetAllPanelIds())
{
    var tabs = _splitViewUIManager.GetPanelTabs(panelId);
    Debug.WriteLine($"Panel {panelId}: {tabs.Count} tabs");
}
```

---

**Last Updated**: 2026-06-12
**Version**: 1.0
