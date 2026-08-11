# Localization

MewUI keeps the user-facing strings that the framework itself renders - message boxes, the busy indicator, the text box context menu, the managed file dialog, and the color picker - in one place: the static `MewUIStrings` class. Every entry defaults to English; assign a new value to translate it. Strings your own app renders are yours to manage - `MewUIStrings` only covers text that MewUI draws on your behalf.

---

## 1. How it works

`MewUIStrings` exposes each string as an `ObservableValue<string>`. Read the current text through `.Value`, and assign `.Value` to change it:

```csharp
MewUIStrings.CommonOK.Value = "확인(_O)";
```

Most framework UI reads these values **when it builds the UI**. Message boxes and the file dialog (including its sidebar) are rebuilt every time they are shown, so setting a value before the UI appears is enough. Changing a value does not retitle a dialog that is already on screen; the next time it opens it picks up the new text.

Standard editing Commands are the exception: they use live bindings. The `CommandPresentation.AccessText` of
`StandardCommands.Cut`, `Copy`, `Paste`, and their peers is bound to `MewUIStrings.CommandCut`, `CommandCopy`,
`CommandPaste`, and so on. Changing one updates open menus and Buttons opted into command presentation immediately.
App Commands can use the same mechanism with `new Command(...).BindText(AppStrings.Save)`.

Set your translations once at startup, and again if the user switches language at runtime, before the next dialog opens.

---

## 2. String groups

Members follow a `{Area}{Role}` naming scheme, where the `Area` prefix marks the group. The groups and what each covers:

| Group (`Area`) | Covers |
| --- | --- |
| `Common` | Shared button labels (OK, Cancel, Yes, No, Retry, Ignore, Abort), reused by message boxes, the busy indicator, and the file dialog. |
| `Prompt` | Message box title text (chosen by icon) and the "Show Details" toggle. |
| `BusyIndicator` | Busy indicator abort confirmation and progress text. Its Abort/Yes/No buttons use the `Common` group. |
| `Command` | Live presentation text for standard editing Commands (`Undo`, `Redo`, `Cut`, `Copy`, `Paste`, `Delete`, `SelectAll`). |
| `TextBoxContextMenu` | Compatibility names for the `Command` entries; they share the same `ObservableValue` instances. |
| `FileDialog` | Managed (in-app) file dialog chrome: window titles, accept buttons, field labels, nav tooltips, view toggles, filter name, and column headers. Its Cancel button reuses `CommonCancel`. |
| `Sidebar` | File dialog sidebar section headers (per-platform conventions). |
| `Folder` | Known-folder shortcut labels in the file dialog sidebar. |
| `ColorPicker` | The hex input label. Channel letters (R/G/B/H/S/V/A) are locale-neutral symbols and are not localized. |

For the exact members in each group and their default values, see `src/MewUI/Core/MewUIStrings.cs` - it is the single source of truth. Type `MewUIStrings.` in your editor to browse them by group prefix.

---

## 3. Access keys (mnemonics)

Some button values contain an underscore, such as `"_OK"` or `"_Save"`. The underscore marks the **access key**: the character right after it becomes the Alt shortcut and is underlined while Alt is held. Keep the underscore in your translation, placing it before the character you want as the shortcut - for example `"확인(_O)"`.

Access keys are a Windows and Linux convention. macOS does not use them (the Option key is reserved for character input), so the underscore is simply dropped there.

---

## 4. Applying a culture

`MewUIStrings` ships English defaults, and `MewUIStrings.ResetToDefaults()` restores every string to that English baseline at any time. Reset to the baseline first, then override the strings for the active culture:

```csharp
static void ApplyStrings()
{
    MewUIStrings.ResetToDefaults(); // English baseline

    switch (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
    {
        case "ko": ApplyKorean(); break;
        // add more cultures here
    }
}

static void ApplyKorean()
{
    MewUIStrings.CommonOK.Value = "확인(_O)";
    MewUIStrings.CommonCancel.Value = "취소(_C)";
    MewUIStrings.PromptError.Value = "오류";
    // strings this culture does not set fall back to the English default
}
```

Call `ApplyStrings` before showing the first window, and again whenever the language changes. Resetting first means every run starts from a complete English set, so any string a culture does not translate - whether it is missing from a partial translation or left over from a previously selected language - stays English rather than showing a stale value. Construction-time consumers pick it up the next time they are shown; command-presentation consumers update immediately.

---

## 5. What is not here

`MewUIStrings` deliberately excludes text that has a better source than a framework string:

- **Culture-driven formatting** - `Calendar` day and month names come from `CultureInfo.CurrentCulture`, so they already follow the OS/app culture without any assignment.
- **OS-owned names** - the file dialog's actual drive and volume labels come from the operating system. Root-volume fallbacks such as "Macintosh HD" and "File System" stay in the platform layer rather than here, because they are OS proper nouns, not translatable UI terms.
- **Your app's own strings** - window titles, labels, and messages you author are outside MewUI's scope; localize them with your own resources.

> Known-folder labels (`Folder*`) default to English. The OS also exposes localized display names for these folders, but those track the OS language rather than your app's `CurrentUICulture`, so MewUI keeps them app-controllable here. Assign them per culture like any other group.
