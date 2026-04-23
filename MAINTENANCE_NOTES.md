# Maintenance Notes

## 2026-04-22

- `LaserAdd_3DPrinting V2.0.sln` temporarily removes `3DPrintControlSetup（LaserADD）.vdproj`.
- Reason: the current Visual Studio environment can open the main software projects, but does not support the legacy `vdproj` installer project by default.
- This is a solution-load compatibility adjustment only. It does not delete or modify the installer project files themselves.
- If installer packaging is needed later, install the Visual Studio extension `Microsoft Visual Studio Installer Projects`, then add `3DPrintControlSetup（LaserADD）.vdproj` back into the solution.
- Related symptom observed during maintenance: after clearing `.vs`, Visual Studio re-evaluated the full solution and blocked loading on the unsupported `vdproj` project.
