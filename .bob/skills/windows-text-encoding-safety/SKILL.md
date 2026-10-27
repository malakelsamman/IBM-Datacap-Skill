---
name: windows-text-encoding-safety
description: Avoid corrupting Arabic or other non-ASCII text when editing files and running commands on Windows PowerShell (mojibake, question marks, BOM and line-ending changes). Use before writing Arabic to any source, JSON or config file, or when console output shows ? for Arabic.
---

# Windows text and encoding safety

## Rules
1. **Never write Arabic through PowerShell strings.** `Set-Content`, `Add-Content`, `Out-File`, here-strings and `-replace` all pass through the console code page and turn Arabic into mojibake or literal `?`.
2. Write non-ASCII text with the edit tools (`apply_diff`, `search_and_replace`, `write_file`) or a Node script that calls `fs.writeFileSync(path, text, 'utf8')`.
3. ASCII-only appends are safe with `Add-Content` (for example plain CSS). If the text has any non-ASCII character, do not use it.
4. **Console `?` is not proof of damage.** The code page prints `?` for Arabic even when the file is correct. Verify with `grep` for a known Arabic word, or read the file with `read_file`.
5. Always parse and write JSON with `utf8`; keep `<?xml ... encoding="UTF-8"?>` headers when you write Datacap XML.

## Symptoms and fixes
| Symptom | Likely cause | Fix |
|---|---|---|
| Portal shows `Ø§Ù„...` | File saved as Windows-1252 over UTF-8 | Rewrite the affected strings with the edit tool, not PowerShell |
| Portal shows `???` | Arabic went through a PowerShell string | Restore from the last good copy, redo with the edit tool |
| A diff marks every line changed | Line endings were normalised (CRLF vs LF) | Harmless; re-check with `node --check` and `grep` |

`server.js` was corrupted once this way and had to be repaired. Check it with `node --check server.js` after any scripted edit.

## Quick checks after editing
- `node --check file.js` for syntax.
- `grep` for a distinctive Arabic label to confirm it survived.
- Hit the page or endpoint and look at the real output, not the console echo.
