# StoryForge Scribe

A quiet, local-first writing desk for drafting a novel. The editor is the page; everything
else — outline, notes, and an optional AI muse — sits around the edges and stays out of the way.

Built as a Blazor Web App (interactive server rendering) on .NET 10. Your manuscript is a plain
JSON file on disk, so nothing here needs an account, a cloud, or a database.

## Running it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet run
```

Then open <http://localhost:5168>. On first run Scribe seeds a sample book,
*The Cartographer's Daughter*, so there is something to read and edit immediately.

## How the book is built

```
Book
├── Unassigned beats          beats written before they have a home
├── Unassigned scenes         scenes waiting for a chapter
│   └── Beat
└── Chapter
    └── Scene      (POV, setting, summary)
        └── Beat   (title, brief, prose, status)
```

- **Beats** are the smallest unit of writing: one turn, one image, one thing that happens.
  A beat holds an outline *brief* and the actual **prose**. Write a beat at any time; it can
  stay loose forever or join a scene later.
- **Scenes** gather beats that share a place and moment. A scene can exist on its own until it
  is ready for a chapter.
- **Chapters** gather finished scenes in reading order.

Organization is non-destructive: moving a beat or scene never changes its prose. Removing a
chapter sends its scenes back to the unassigned pool, and removing a scene sends its beats back
to the unassigned beats pool.

Every beat carries a status — Outline, Draft, Revised, or Final — shown as a coloured dot in the
outline and in the editor.

## Writing

- The Edit Window fills the workspace: title, status, POV, an optional brief, then the prose.
- **Autosave** runs about 1.2 s after you stop typing (and at least every 15 s while you keep
  going). The save indicator in the topbar tells you where things stand.
- **Backups** are written every 15 minutes while the manuscript is changing, plus on demand and
  automatically before anything destructive (restoring, starting a new book). The newest 96 are
  kept. Open **Backups** from the topbar to browse, compare timestamps, and restore.
- Reorder beats, scenes, and chapters from the outline; use the search box to filter by title,
  brief, or prose.

### Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+S` | Save now |
| `Ctrl+Alt+F` | Toggle focus mode |
| `Ctrl+Alt+A` | Toggle the AI muse |
| `Alt+↑` / `Alt+↓` | Previous / next beat |
| `Esc` | Close menus and dialogs |

## The AI muse (optional)

Out of the box Scribe is **offline**: the muse panel still gives craft prompts and structural
suggestions, with no network calls. To connect a model, open **AI settings** from the muse panel
or the topbar:

- **OpenAI-compatible** endpoints (OpenAI, OpenRouter, Gemini's compatibility API, Ollama,
  LM Studio, or anything else that speaks `/chat/completions`), or
- **Anthropic** (Messages API).

Set the base URL, model, key, temperature, token limit, and reasoning effort. Settings are stored in
`App_Data/settings.json`. Environment variables override the saved key and always win:

```powershell
$env:STORYFORGE_AI_API_KEY = "..."   # any provider
$env:OPENAI_API_KEY = "..."          # OpenAI-compatible providers
$env:ANTHROPIC_API_KEY = "..."       # Anthropic
```

Thinking models need one extra knob. Qwen 3/3.5 and similar models can spend the entire
response budget on hidden reasoning and return no prose. Set **Reasoning effort** to
**None** in AI settings (the Ollama preset does this automatically); the field is omitted
for providers that don't support it.

Available muse actions: continue the current beat, draft a beat from its brief, brainstorm
beats, tighten prose, describe the setting, critique the scene, or ask a freeform question.
Output streams into the panel, where you can append it to the beat, replace the beat (or just the
text you had selected), copy it, or undo the last edit.

## Where your work lives

Everything sits under `App_Data/` next to the app:

```
App_Data/
├── book.json                  the manuscript (written atomically)
├── settings.json              AI connection settings
└── backups/
    └── book-YYYYMMDD-HHmmss-fff-<reason>.json
```

Configuration lives in `appsettings.json`:

```json
"Storage": {
  "DataDirectory": "App_Data",
  "AutosaveDebounceMs": 1200,
  "BackupIntervalMinutes": 15,
  "MaxBackups": 96
}
```

`App_Data/` is deliberately ignored by git — it is your writing, not source.

## Exports and endpoints

- `GET /export/manuscript.md` — the written beats as Markdown, with chapter and scene headings.
- `GET /export/book.json` — the full book document, for backup or portability.
- `GET /health` — a small JSON status: title, word count, save state, last backup time.

## Development

```powershell
dotnet build
dotnet run
```

Project layout:

```
Components/
├── Pages/Write.razor          the workspace: commands, shortcuts, dialogs
├── Workspace/                 BookOutline, EditorPane, AiPanel
├── Dialogs/                   book, entity, confirm, backups, shortcuts, AI settings
└── Shared/                    Modal, Icon, ToastHost
Domain/                        models, word counting, outline helpers, Markdown export
Services/                      persistence, autosave/backup workers, seeding, AI clients
wwwroot/                       app.css design system, js/scribe.js interop
```

The app is single-user and local by design: one manuscript per `App_Data` folder. To keep more
than one book, copy the folder or point `Storage:DataDirectory` somewhere else.
