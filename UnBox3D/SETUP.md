# UnBox3D - Developer Setup Guide

## Prerequisites

### 1. Install Blender 4.2 LTS (REQUIRED)

**Do NOT use Blender 4.5 or 5.0** — the Export Paper Model addon is only compatible with 4.2.

1. Download from: https://www.blender.org/download/lts/4-2/
2. Install to the default location: `C:\Program Files\Blender Foundation\Blender 4.2\`

### 2. Install the Export Paper Model Addon (REQUIRED)

1. Open **Blender 4.2** (not any other version)
2. Go to **Edit → Preferences → Get Extensions**
3. Search for **"Export Paper Model"**
4. Click **Install**
5. Make sure the addon checkbox is **enabled** ✓
6. **Close Blender**

### 3. Patch the Addon Bug (REQUIRED)

The addon has a bug with flat geometry. You must patch one line:

1. Navigate to:
   ```
   %APPDATA%\Blender Foundation\Blender\4.2\extensions\user_default\export_paper_model\unfolder.py
   ```
2. Find this line (~line 375):
   ```python
   balance = sum((+1 if edge.angle > 0 else -1) for edge in island_edges)
   ```
3. Replace it with:
   ```python
   balance = sum((+1 if (edge.angle or 0) > 0 else -1) for edge in island_edges)
   ```
4. Save the file

**Or run this PowerShell command to patch automatically:**
```powershell
$file = "$env:APPDATA\Blender Foundation\Blender\4.2\extensions\user_default\export_paper_model\unfolder.py"
(Get-Content $file -Raw).Replace(
    'balance = sum((+1 if edge.angle > 0 else -1) for edge in island_edges)',
    'balance = sum((+1 if (edge.angle or 0) > 0 else -1) for edge in island_edges)'
) | Set-Content $file -NoNewline
```

### 4. Build and Run

1. Open `UnBox3D.sln` in Visual Studio
2. Build the solution (Ctrl+Shift+B)
3. Run (F5)

The app will automatically detect Blender 4.2 from Program Files on startup.

## Troubleshooting

| Error | Cause | Fix |
|-------|-------|-----|
| "Blender executable not found" | Blender 4.2 not installed | Install Blender 4.2 LTS |
| "Addon not loaded" | Export Paper Model addon not installed | Install addon in Blender 4.2 Preferences |
| "NoneType > int" error | Addon not patched | Apply the unfolder.py patch above |
| "Model was too complex" | Old error message (should not appear) | Pull latest code |
| SVG file not generated | Output path issue | Save to a folder like Desktop, not the build output folder |
