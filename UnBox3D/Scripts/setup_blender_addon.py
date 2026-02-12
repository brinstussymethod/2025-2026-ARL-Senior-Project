"""
Blender Addon Setup Script
Automatically enables the Export Paper Model addon in Blender.
This script should be run once during app initialization.
"""
import bpy
import sys

def enable_paper_model_addon():
    """
    Enables the export_paper_model addon if available.
    Returns True if successful, False otherwise.
    """
    # Try multiple possible addon names (Blender 4.2 uses extensions system)
    possible_names = [
        "bl_ext.user_default.export_paper_model",
        "bl_ext.blender_org.export_paper_model", 
        "export_paper_model"
    ]

    addon_name = None
    # Find which name is actually installed
    for name in possible_names:
        if name in [mod.__name__ for mod in __import__('addon_utils').modules()]:
            addon_name = name
            print(f"Found addon with name: {addon_name}")
            break

    if not addon_name:
        addon_name = possible_names[0]  # Default to first option
    
    print("=" * 60)
    print("BLENDER ADDON SETUP")
    print("=" * 60)
    
    # Check if already enabled
    if addon_name in bpy.context.preferences.addons:
        print(f"✓ Addon '{addon_name}' is already enabled.")
        print("=" * 60)
        return True
    
    # Try to enable the addon
    print(f"Attempting to enable addon '{addon_name}'...")
    try:
        bpy.ops.preferences.addon_enable(module=addon_name)
        
        # Save user preferences so addon stays enabled
        bpy.ops.wm.save_userpref()
        
        print(f"✓ SUCCESS: Addon '{addon_name}' has been enabled!")
        print(f"✓ User preferences saved.")
        print("=" * 60)
        return True
        
    except Exception as e:
        print(f"✗ ERROR: Failed to enable addon '{addon_name}'")
        print(f"  Reason: {e}")
        print("")
        print("POSSIBLE SOLUTIONS:")
        print("1. The addon may not be installed in this Blender version")
        print("2. Manual installation required:")
        print("   - Open Blender GUI")
        print("   - Edit → Preferences → Add-ons")
        print("   - Search for 'paper' or 'Export Paper Model'")
        print("   - Enable the checkbox")
        print("=" * 60)
        return False

if __name__ == "__main__":
    success = enable_paper_model_addon()
    sys.exit(0 if success else 1)
