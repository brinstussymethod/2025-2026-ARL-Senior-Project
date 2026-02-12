"""
Test script to check addon compatibility with Blender version
"""
import bpy
import sys
import zipfile
import tempfile
import os

print("=" * 60)
print("BLENDER ADDON COMPATIBILITY CHECKER")
print("=" * 60)
print(f"Blender Version: {bpy.app.version_string}")
print(f"Blender Version Tuple: {bpy.app.version}")
print("")

# Check if addon file path was provided as argument
if len(sys.argv) > 1 and '--' in sys.argv:
    args = sys.argv[sys.argv.index('--') + 1:]
    if len(args) > 0:
        addon_path = args[0]
        print(f"Testing addon: {addon_path}")
        
        # Try to read addon metadata
        try:
            if addon_path.endswith('.zip'):
                with zipfile.ZipFile(addon_path, 'r') as zip_ref:
                    # Look for __init__.py or bl_info
                    for name in zip_ref.namelist():
                        if '__init__.py' in name:
                            content = zip_ref.read(name).decode('utf-8')
                            if 'bl_info' in content:
                                print("\nFound bl_info in addon:")
                                # Try to extract version info
                                lines = content.split('\n')
                                in_bl_info = False
                                for line in lines:
                                    if 'bl_info' in line:
                                        in_bl_info = True
                                    if in_bl_info:
                                        if 'blender' in line.lower() or 'version' in line.lower():
                                            print(f"  {line.strip()}")
                                        if '}' in line:
                                            break
                            break
        except Exception as e:
            print(f"Error reading addon: {e}")

print("")
print("=" * 60)
print("RECOMMENDATION:")
print("For Blender 4.2, look for addon releases from 2023-2024")
print("Blender 4.2 was released in late 2023")
print("=" * 60)
