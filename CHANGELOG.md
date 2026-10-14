# 📜 Changelog

All notable changes to the **AgduBugdu Editor** project will be documented in this file. ✨

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). 🏷️

---

## 🚀 [1.3.2] - 2026-10-14

### ✨ Added
- **🛡️ Cloud Licensing & Entitlement System**:
  - Centralized software licensing and activation engine integrated with the high-performance **Licensor** backend API (`https://licensor-h5zdysrkqa-uc.a.run.app`).
  - **Tiered License Levels**:
    - **👑 Admin (`ADM-xxxx-xxxx-xxxx`)**: Highest privilege tier. Bypasses username and password checks; binds machine HWID on first use and enforces seat limits.
    - **⚡ Dev (`DEV-xxxx-xxxx-xxxx`)**: Internal developer/partner tier. Validates username, password hash, HWID node-lock, and duration limits.
    - **👤 User (`USER-xxxx-xxxx-xxxx`)**: Retail and subscriber tier. Validates username, password hash, HWID node-lock, and subscription expiry.
    - **⏳ Trial**: Automatic 60-day local evaluation license provided out of the box when no paid key is active.
  - **Ascending Stylized License Badges**:
    - *Admin*: Luxurious gold and amber metallic aura (`#FDE047` border with glowing golden halo).
    - *Dev*: Vibrant neon cyan and deep indigo gradient (`#38BDF8` border with cyan glow).
    - *User*: Polished emerald green badge with verified identity tag.
    - *Trial*: Understated dark slate badge displaying real-time days remaining.
  - **🔒 License-Locked Extensions in Trial Mode**:
    - In local Trial mode, extensions (Live PDF Viewer & Editor, Live Data Grid, Run & Debug Workbench, Workspace TODO Explorer, language tools) are safely locked.
    - Activating any valid paid license (`User`, `Dev`, or `Admin`) instantly unlocks and activates all extensions across the editor in real-time.
  - **Persisted Registry Storage**:
    - Stores license state, HWID, and trial baseline in the Windows Registry (`HKCU\Software\AgduBugdu\License`).
    - State persists seamlessly across uninstalls, re-installs, and version updates to prevent trial tampering and accidental license loss.
  - **Interactive License Modal & Status Bar**:
    - Accessible via `Help -> Software License...`, the Status Bar badge pill, or the Command Palette (`Ctrl+P` -> `Help: Software License & Entitlements...`).
    - Features masked key entry with a 1-click **Reveal/Hide** toggle, dynamic credential inputs, live HWID fingerprint display, and a 1-click **Remove License** button that frees server seats and cleanly reverts to Trial mode.

### 🐛 Fixed
- **Fatal UI Startup Crash**: Fixed an unhandled `ArgumentException` on application initialization caused by an unrecognized `KeyGesture` backtick character in the Terminal menu; resolved to standard Avalonia `Ctrl+Shift+OemTilde`.

---

## 🚀 [1.3.1] - 2026-10-14

### ✨ Added
- **⚡ In-App Seamless Auto-Updater**:
  - Comprehensive end-to-end in-app automatic updater with streaming download progress, cryptographic integrity verification, and 1-click install & clean restart.
  - **Platform & Architecture Resolution**: Automatically queries GitHub Releases API to detect and select the appropriate platform binary (`AgduBugdu-v*-win-x64-setup.exe` on Windows, `.tar.gz` on Linux/macOS, architecture-aware for ARM64 and x64).
  - **Asynchronous Streaming Download**: Robust 80 KB chunked streaming pipeline saving downloads directly to `%TEMP%/AgduBugdu_Updates/` with real-time throughput metrics (downloaded MB / total MB, progress percentage, transfer speed in MB/s), and graceful cancellation.
  - **Cryptographic Checksum Verification**: Automatically computes the SHA-256 hash of the downloaded package and verifies it against published `SHA256SUMS.txt` prior to execution, preventing corrupted or modified installations.
  - **Interactive Auto-Update UI Dialog**: Redesigned modern update modal (`UpdateModalView`) with multi-state transitions:
    - *Available*: Highlights version upgrade diffs, release notes, and detected package size with a 1-click "Download & Install" action and fallback "View on GitHub".
    - *Downloading*: Active animated progress bar with live download stats and "Cancel Download" option.
    - *Ready to Install*: Green verified status banner and high-visibility "Restart & Install" action that launches the Inno Setup installer cleanly and terminates the editor without file lock contention.
    - *Error Diagnostics*: Graceful error state reporting with network retry and manual release fallback.
  - **Automated Update Checks**: Background silent update discovery on editor launch and manual trigger via "Help -> Check for Updates...".

---

## 🚀 [1.3.0] - 2026-10-14

### ✨ Added
- **📑 Live PDF Viewing & Editing Extension (`AgduBugdu.Plugin.PdfLive`)**:
  - High-performance, memory-efficient PDF document parser, page layout inspector, and editor backed by `PdfPig` and `PdfSharpCore`.
  - **Document Inspection**: Extracts detailed document metadata (title, author, subject, keywords, producer, creation/modification dates), page counts, dimensions, orientations, and interactive bookmark / outline tree hierarchies.
  - **Full-Text In-Document Search**: High-speed text extraction and search across all pages with match snippet extraction.
  - **Interactive Editing & Page Manipulation**:
    - **Page Rotation**: Rotate single or multiple pages clockwise (90°, 180°, 270°).
    - **Page Deletion**: Remove individual pages or ranges with automatic layout re-indexing.
    - **Document Splitting & Page Extraction**: Extract individual pages or custom ranges to separate standalone PDF files.
    - **Multi-Document Merging**: Combine multiple PDF files into unified documents.
    - **Diagonal Watermarking**: Apply customizable text watermarks across pages with opacity and font sizing.
    - **Metadata Editor**: Update and persist Title, Author, Subject, and Keywords.
  - Seamless IDE integration with Workspace Explorer context menu ("Open in Live PDF Viewer & Editor"), automatic `.pdf` file association, and tabbed preview renderer.
- **🔍 Workspace Search / Find & Replace Across Files (`Ctrl+Shift+F`)**:
  - Comprehensive multi-file text search engine (`WorkspaceSearchEngine`) with asynchronous cancellation support and real-time progress.
  - Configurable filter options: Match Case (`Aa`), Match Whole Word (`|ab|`), and Regular Expressions (`.*`).
  - Wildcard file include patterns (`*.cs, *.axaml, *.ts`) and exclude patterns (`bin, obj, node_modules, *.min.js`).
  - Smart binary file skipping (null-byte analysis & file extension checks).
  - Global Workspace Replace: single-file replace and workspace-wide "Replace All" with replacement counts and files-modified metrics.
  - Interactive Dockable UI: TreeView grouped by file with result counters and double-click navigation straight to file line and column.
- **🌿 Git & Source Control Workbench (`Ctrl+Shift+G`)**:
  - Cross-platform native Git integration with real-time status tracking (`git status --porcelain=v1 -b`).
  - Shows active branch with tracking indicators (`ahead` / `behind` commits).
  - Change Lists: Grouped into **Staged Changes** and **Working Tree Changes** with status badges (`M`odified, `A`dded, `D`eleted, `U`ntracked, `R`enamed, `C`opied, Conflict `!`).
  - Staging controls: Stage/Unstage individual files (`+` / `-`), Stage All, Unstage All, and Discard Changes (`↩`).
  - In-app Commit: Commit box supporting `Ctrl+Enter` dispatch with error handling and commit hash reporting.
  - Diff View: Inline diff inspection via `git diff` and `git diff --cached`.
  - Auto-refreshes on workspace folder changes and file saves.
- **📊 Interactive Status Bar & UI Navigation**:
  - **Interactive Git Branch Pill**: Left-aligned clickable branch button with leaf icon (`🌿`) and change count badge; clicking immediately opens and focuses the Source Control panel.
  - **Clickable Caret Navigation (`Ln X, Col Y`)**: Clicking the line/column indicator in the status bar directly launches the Go to Line / Column modal (`Ctrl+G`).
  - Dynamic controls for Line Endings (`CRLF`/`LF`), Indentation (`Spaces: 2/4/8`, `Tabs`, whitespace conversion), and File Encodings (`UTF-8`, `UTF-8-BOM`, `UTF-16LE`, `ASCII`).
- **📂 Recent Files & Workspaces Persistence**:
  - Thread-safe JSON persistence tracking recent workspaces and files in `%LOCALAPPDATA%/AgduBugdu/recent_items.json`.
  - Dynamic `File -> Open Recent` submenu in menu bar displaying recent workspace directories (`📁`), files (`📄`), and "🧹 Clear Recently Opened".
  - Integrated into Command Palette for quick lookup and clearing.

### 🐛 Fixed
- **⌨️ Menu & Command Palette Hotkeys**: Fixed missing keyboard shortcuts `Ctrl+Shift+F` (Search), `Ctrl+Shift+G` (Source Control), `Ctrl+Shift+U` (Uppercase), `Ctrl+U` (Lowercase), `Shift+Alt+Up` (Duplicate Line Up), `Shift+Alt+Down` (Duplicate Line Down), and `Ctrl+Shift+J` (Join Lines).
- **🌐 About Dialog Hyperlinks**: Fixed About Modal GitHub repository button click handler to properly open the browser.
- **💻 Terminal Prompt Alignment**: Fixed terminal output margin alignment and session restart on directory switch.
- **📑 Clean File Modification State**: Fixed Welcome document being falsely marked dirty upon initial launch.

---

## 🚀 [1.2.0] - 2026-10-04

### ✨ Added
- **🔍 In-Buffer Find & Replace Overlay (`Ctrl+F` / `Ctrl+H`)**:
  - Interactive non-blocking search and replace bar docked directly inside active editor tabs.
  - Case Sensitive (`Aa`), Match Whole Word (`\b`), and Regular Expressions (`.*`) toggles.
  - Previous (`Shift+Enter` / `Shift+F3`) and Next (`Enter` / `F3`) match navigation with match count indicators (`X of Y`).
  - Selective Single Replace and global Replace All with instant buffer updates.
  - Smooth dismiss via `Escape` key returning focus seamlessly to the editor caret.
- **📍 Go to Line / Column Dialog (`Ctrl+G`)**:
  - Modal navigation prompt accepting `line` or `line:column` (or `line,column`) formats with live boundary validation against document line limits.
  - Automatically centers viewport and positions caret at the target destination.
- **🔲 Bracket & Quote Matching & Auto-Closing**:
  - **Go to Matching Bracket (`Ctrl+M`)**: Instant caret jump between opening and closing delimiters for `()`, `[]`, `{}`, and `<>`.
  - **Auto-Closing Delimiters**: Automatic insertion of closing partner characters for parentheses `()`, brackets `[]`, braces `{}`, double quotes `""`, single quotes `''`, and backticks ` `` `.
  - **Selection Wrapping**: Typing an opening bracket or quote with text selected wraps the selection rather than overwriting it.
  - **Overtype Skipping**: Typing a closing character immediately preceding an identical closing partner moves the caret past it without double-inserting.
- **⚡ Advanced Editing Ergonomics & Line Manipulations**:
  - **Toggle Line Comment (`Ctrl+/` or `Ctrl+K, Ctrl+C` / `Ctrl+K, Ctrl+U`)**: Language-aware line commenting/uncommenting supporting 25+ language extensions and filetypes.
  - **Toggle Block Comment (`Shift+Alt+A`)**: Delimited multi-line block commenting (e.g. `/* ... */`, `<!-- ... -->`, `"""..."""`).
  - **Move Line(s) Up / Down (`Alt+Up` / `Alt+Down`)**: Relocate single lines or multi-line selections with automatic boundary handling and selection preservation.
  - **Duplicate Line(s) Up / Down (`Shift+Alt+Up` / `Shift+Alt+Down`)**: Instant line and block duplication without clobbering the system clipboard.
  - **Delete Line (`Ctrl+Shift+K`)**: Instant current line removal with automatic caret repositioning.
  - **Join Lines (`Ctrl+Shift+J`)**: Merge consecutive lines with normalized whitespace separation.
  - **Case Transformations (`Ctrl+Shift+U` / `Ctrl+U`)**: Transform selected text (or word under cursor) to UPPERCASE or lowercase.
  - **Word Wrap Toggle (`Alt+Z`)**: Dynamic soft line wrapping toggle per editor tab.
- **📂 Code Folding (`FoldingManager`)**:
  - Integrated `AvaloniaEdit.Folding` manager providing fold/unfold gutters for multi-line block constructs (`{...}`).
  - Automatically recalibrated on text edits with collapsed preview indicators.
- **📊 Interactive Status Bar Controls & Formatters**:
  - **Line Endings Switcher (`CRLF` / `LF`)**: Clickable flyout on status bar enabling on-the-fly conversion of document line endings.
  - **Indentation & Tab Size Switcher**: Clickable flyout supporting `Spaces: 2`, `Spaces: 4`, `Spaces: 8`, `Tab Size: 2`, `Tab Size: 4`, `Tab Size: 8`, alongside one-click "Convert Indentation to Spaces" and "Convert Indentation to Tabs".
  - **File Encoding Switcher**: Clickable flyout supporting `UTF-8`, `UTF-8 with BOM`, `UTF-16 LE`, and `ASCII`.
  - **Word Wrap Switcher**: Clickable status bar badge toggling wrapping mode.

---

## 🌐 [1.1.0] - 2026-10-04

### ✨ Added
- **🌐 Dedicated Language Support Extensions for 6 Core Languages**:
  - **⚡ C/C++ Language Support** (`AgduBugdu.Plugin.Cpp`): TextMate syntax highlighting for `source.cpp` and `source.c` across `.cpp`, `.c`, `.h`, `.hpp`, `.cc`, `.cxx`, `.hh`; file creation templates (`main.cpp`, `main.c`); comment rules (`//`, `/* */`); and rich C++ snippets (`main`, `cmain`, `class`, `struct`, `fori`, `forr`, `cout`, `cin`, `guard`, `pragma`, `vec`, `try`, `lambda`).
  - **☕ Java Language Support** (`AgduBugdu.Plugin.Java`): TextMate syntax highlighting for `source.java` across `.java`; class creation templates (`Main.java`); comment rules; and rich Java snippets (`main`, `class`, `sout`, `serr`, `fori`, `foreach`, `interface`, `record`, `try`, `singleton`, `test`).
  - **🐹 Go Language Support** (`AgduBugdu.Plugin.Go`): TextMate syntax highlighting for `source.go` across `.go`; package creation templates (`main.go`); comment rules; and idiomatic Go snippets (`main`, `func`, `meth`, `struct`, `interface`, `iferr`, `go`, `forr`, `test`, `http`).
  - **🦀 Rust Language Support** (`AgduBugdu.Plugin.Rust`): TextMate syntax highlighting for `source.rust` across `.rs`; module creation templates (`main.rs`); comment rules; and idiomatic Rust snippets (`main`, `fn`, `struct`, `enum`, `impl`, `test`, `match`, `pln`, `forin`, `tokiomain`).
  - **🐍 Python Language Support** (`AgduBugdu.Plugin.Python`): TextMate syntax highlighting for `source.python` across `.py` and `.pyw`; script creation templates (`main.py`); comment rules (`#`, `"""`); and modern Python snippets (`main`, `def`, `class`, `dataclass`, `try`, `withopen`, `lcomp`, `pf`, `test`, `fastapi`).
  - **🔷 C# / .NET Language Support** (`AgduBugdu.Plugin.CSharp`): TextMate syntax highlighting for `source.cs` across `.cs` and `.csx`; program creation templates (`Program.cs`); comment rules; and modern C# snippets (`class`, `prop`, `propg`, `ctor`, `record`, `interface`, `asyncm`, `cw`, `try`, `topmain`, `fact`, `di`).
- **🧩 Language Subsystem in Contracts & Host** (`ILanguageService`):
  - Added `LanguageDefinition` record capturing Language ID, Name, GrammarScope, FileExtensions, and comment symbols.
  - Added `Snippet` record capturing ID, Name, Prefix, Description, Body, LanguageId, and searchable Tags.
  - Added `ILanguageService` contract to `AgduBugdu.PluginContracts` and implemented `DefaultLanguageService` in `AgduBugdu.Extensibility`.
  - Added `InsertText`, `GetActiveDocumentText`, `SetActiveDocumentText`, and `NewDocument` to `IEditorService`.
  - Exposed `ILanguageService Languages { get; }` through `IExtensionContext`.
- **⚡ Command Palette & Editor Integration**:
  - Over 70 code snippets registered directly into the Command Palette (`Ctrl+P`), enabling instant lookup and insertion into active buffers.
  - New file commands for every language (`File: New C++ Source File`, `File: New Java Source File`, `File: New Go Source File`, `File: New Rust Source File`, `File: New Python Script`, `File: New C# Program File`).
  - All 6 language extensions listed and tracked in `View -> Extensions...` modal.

---

## 🛠️ [1.0.2] - 2026-10-03

### 🐛 Fixed
- **🗔 View Panes Visibility & Navigation Restoration**: Resolved layout issue where ToolDock alignment overrides caused Explorer, Terminal, Output, TODO Tasks, and Run & Debug screens to disappear from the main docking container; restored inline proportional rendering with full tab restoration on View menu and hotkey navigation.
- **🏷️ Asterisk Modification Indicator**: Fixed tab titles appending `*` prematurely upon opening clean files or the Welcome tab; now uses dual-condition dirty buffer tracking via AvalonEdit's `UndoStack.IsOriginalFile` and original content comparison, strictly showing `*` only when unsaved edits are present and instantly clearing upon save or revert.
- **🚀 Startup / Launch Crash Fix**: Fixed immediate launch crash caused by unsupported raw backtick key in Avalonia UI menu `InputGesture="Ctrl+`"`; updated gesture binding to `Ctrl+OemTilde` with regression test suite coverage.
- **🗕 Pane Minimize Button**: Fixed the minimize button on Terminal, Output, TODO Tasks, and Run & Debug pane headers to strictly display only the glyph (`🗕`) without text, and actively collapse the bottom panel to yield 100% vertical viewport space to active document tabs.
- **📐 Reset Panel to Default Sizes**: Completely overhauled layout reset (`View -> Reset Panel to Default Sizes` and Command Palette `View: Reset Panel to Default sizes`) to cleanly recompute the proportional docking hierarchy back to exact default proportions (22% Explorer, 72% Editor, 28% Bottom Panel) while seamlessly preserving all open document tabs and active tool instances.
- **⌨️ View Navigation & Panel Shortcuts**: Added `View -> Toggle Bottom Panel` (`Ctrl+J`), `View -> Explorer` (`Ctrl+Shift+E`), `View -> Run & Debug` (`Ctrl+Shift+D`), automatically restoring and focusing panes whenever a tool tab or hotkey is invoked.
- **🧹 Code Consolidation & Cleanup**: Cleaned up dock factories and tool view models, normalized XAML button definitions, removed duplicate menu items, and eliminated stale proportion cache issues.

### ✨ Added
- **📊 CSV / TSV Data Table Viewer Plugin** (`AgduBugdu.Plugin.DataGridLive`): RFC 4180 compliant tabular viewer with search/filtering, delimiter auto-detection (comma, tab, semicolon, pipe), and contextual file launch from Workspace Explorer.
- **📋 Workspace TODO & Task Explorer Plugin** (`AgduBugdu.Plugin.TodoExplorer`): Project-wide comment scanner indexing `TODO`, `FIXME`, `BUG`, `HACK`, `NOTE` markers with color badges and double-click jump-to-source navigation.
- **🐞 Run & Debug Workbench Plugin** (`AgduBugdu.Plugin.Debugger`): Interactive debugger pane with breakpoint toggles (`F9`), active execution line highlighting, step controls (`F5`, `F10`, `F11`), call stack, watch variables, and debug console.

---

## 🛠️ [1.0.1] - 2026-10-02

### 🐛 Fixed
- **🏷️ Asterisk Modification Indicator**: Fixed tab titles appending `*` prematurely upon opening clean files; now accurately tracks buffer modifications against initial file content and clears upon save or undo.
- **🗕 Pane Minimize Button & Dynamic Layout Re-render**: Fixed the `🗕 Minimize` button on Terminal and Output panes to actively cycle and re-render the dock layout via UI thread dispatcher, restoring panes back to their original 28% height dimensions.
- **📰 Document & Tool Tab Title Visibility**: Added global dock styling for `DocumentTabStripItem` and `ToolTabStripItem` foregrounds ensuring tab titles are always clearly visible in all states (normal, hovered, and selected) rather than only on hover.
- **📝 Editor Text Visibility & Editing**: Included missing `AvaloniaEdit` Fluent theme styles (`avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`) in `App.axaml` and linked property synchronization, restoring complete text rendering and editing capabilities in editor tabs.
- **🚪 Clean Terminal Exit**: Added graceful session handling when typing `exit` in the terminal pane, cleanly terminating the underlying process and notifying the user.

### ✨ Added
- **🗕 Pane Minimize Button & Layout Reset**: Added a dedicated `🗕 Minimize` button to both Terminal and Output headers, and `View -> Reset Layout Sizes` (with Command Palette command), restoring panes back to their original default dimensions.
- **🧩 Extensions Manager Modal**: Added `View -> Extensions...` modal dialog tracking all loaded and active extensions (including the built-in Markdown Live Viewer plugin) with ID, status, version, and description.
- **👁️ Live Markdown Preview Side by Side**: Added a context menu option in the Workspace Explorer (`Live Markdown Preview (Side by Side)`) that appears specifically and exclusively for `.md` files, opening real-time HTML rendered preview in an adjacent tab.

---

## 🌟 [1.0.0] - 2026-10-01

### 🚀 Added
- **🖥️ Core Architecture & Modern IDE Shell**:
  - Avalonia 11 LTS (`11.3.2`) + `FluentAvaloniaUI` integration with custom dark chrome and responsive layouts.
  - Multi-panel docking system (`Dock.Avalonia`) supporting center editor tabs, left file explorer, and bottom terminal/output panes.
  - Interactive Command Palette overlay (`Ctrl+P` / `Ctrl+Shift+P`) with dynamic command filtering, hotkey gestures, and keyboard navigation.
  - Informative bottom status bar tracking cursor line/column, encoding, theme mode, and workspace state.

- **📝 Advanced Text Editing**:
  - `Avalonia.AvalonEdit` editor core backed by rope data structures for handling large files smoothly.
  - TextMate syntax highlighting (`AvaloniaEdit.TextMate`) with `DarkPlus` theme and automatic file extension detection across 100+ languages.
  - Full native file I/O: Open (`Ctrl+O`), Save (`Ctrl+S`), Save As (`Ctrl+Shift+S`), and New File (`Ctrl+N`).
  - Active caret tracking, dirty buffer change indicators (`*`), font zoom (`Ctrl + MouseWheel`), and editor context menu (Cut, Copy, Paste, Select All).

- **📁 Workspace File Explorer**:
  - Hierarchical workspace explorer with lazy-loading directory expansion to maintain instant responsiveness on deep directory structures.
  - Native folder selection via Avalonia `StorageProvider.OpenFolderPickerAsync` (`Ctrl+K, Ctrl+O`).
  - Double-click file opening into dock tabs.

- **🔌 Extensibility Subsystem & Sample Plugin**:
  - Plugin runtime isolation via collectible `PluginLoadContext` (`AssemblyLoadContext`).
  - Shared contracts library (`AgduBugdu.PluginContracts`) exposing `ICommandRegistry`, `IToolWindowRegistry`, `IEditorService`, and `IWorkspaceService`.
  - Built-in end-to-end sample plugin: `AgduBugdu.Plugin.MarkdownLive`, providing live Markdown-to-HTML rendering and preview docked alongside editor documents.

- **💻 Integrated Interactive Terminal**:
  - Embedded cross-platform process shell hosting (`powershell.exe` on Windows, `bash` on Unix).
  - Terminal tool pane with real-time streaming, command input line, clear screen, and `Ctrl+OemTilde` restart shortcut.
  - Dynamic workspace synchronization: automatically restarts and re-targets working directory to newly opened workspace folders.

- **🎨 All-Encompassing Themes**:
  - **💜 Lonely Dark**: Neon violet accents with deep obsidian background tones.
  - **🌊 Solarized Contrast**: High-contrast solarized cyan and deep petrol teal theme.
  - Dynamic theme switching accessible via menu bar (`Preferences -> Color Theme`) and Command Palette.

- **🔄 Auto-Updater Modal**:
  - Integrated GitHub Releases update checker querying repository releases asynchronously.
  - Interactive modal dialog showing version comparison, scrollable release notes, and direct download links.
  - Manual update trigger under `Help -> Check for Updates...`.

- **📦 Branding, Packaging & Multi-Platform CI/CD**:
  - Custom AI-generated high-resolution app icon and logo assets (`agdubugdu-logo.ico`, `agdubugdu-logo.png`).
  - Centralized global versioning via `Directory.Build.props` (`1.0.0`).
  - Dedicated runner scripts: `tools/run.ps1` (PowerShell) and `tools/run.sh` (POSIX / Cygwin / MSYS2).
  - Multi-platform GitHub Actions Release workflow (`.github/workflows/release.yml`) producing:
    - 🪟 Windows: `AgduBugdu-v1.0.0-win-x64-setup.exe` (Inno Setup) & `AgduBugdu-v1.0.0-win-x64-portable.zip`
    - 🐧 Linux: `AgduBugdu-v1.0.0-linux-x64.tar.gz`
    - 🍎 macOS: `AgduBugdu-v1.0.0-osx-x64.tar.gz` & `AgduBugdu-v1.0.0-osx-arm64.tar.gz`
    - Automatic SHA-256 checksums (`SHA256SUMS.txt`).
