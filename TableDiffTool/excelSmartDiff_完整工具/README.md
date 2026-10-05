# Excel Smart Diff for P4V

This folder contains a local ExcelMerge build with Smart Table Diff enabled and a P4V wrapper for spreadsheet diffs.

## Install

1. Close all P4V windows.
2. Run `install_p4v_excel_smart_diff.cmd`.
3. Reopen P4V.

The installer updates the current user's `%USERPROFILE%\.p4qt\ApplicationSettings.xml` and backs it up before editing.

## P4V Associations

The installer configures these file extensions:

- `xlsx`
- `xls`
- `xlsm`
- `csv`
- `tsv`

Application:

```text
Tools\p4_helper\excelSmartDiff\p4v-diff\ExcelMergeP4VDiff.exe
```

Arguments:

```text
%1 %2 --open
```

## Notes

- `p4v-diff\ExcelMergeP4VDiff.exe` copies P4V temporary files into `%LOCALAPPDATA%\ExcelSmartDiff\p4v-diff\cache` before launching ExcelMerge, so ExcelMerge does not lose access to short-lived P4 temp files.
- The right side of a P4V diff is editable for `.xls` and `.xlsx` files. Edits are applied to the cached working copy first; use `Save` or `Ctrl+S` in ExcelMerge to write the changes back to the real right-side workspace file.
- Before saving edits back to the workspace file, ExcelMerge writes a backup under `%LOCALAPPDATA%\ExcelSmartDiff\p4v-edit-backup`.
- Edit v1 supports cell edit, cell clear, and rectangular paste. Edit v2 supports row copy, insert blank row, duplicate row, and delete row from the right-side grid. Column structure editing is intentionally not enabled.
- Wrapper logs are written to `%LOCALAPPDATA%\ExcelSmartDiff\p4v-diff\ExcelMergeP4VDiff.log`.
- On each launch, the wrapper removes cache folders older than 3 days. If the cache grows past 100 session folders, it keeps the newest 50 folders and removes the rest.
- `_p4ignore.txt` still ignores legacy workspace cache/log paths as a safety net, but normal runtime cache and logs should not be written under this P4 workspace.
- The wrapper is built as a small .NET Framework executable. ExcelMerge itself still runs from `app\ExcelMerge.GUI.exe`.
