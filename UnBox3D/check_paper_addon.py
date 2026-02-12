import bpy
print("=== Checking Blender for Paper Model Support ===")
print(f"Blender version: {bpy.app.version_string}")

# Check for addon
try:
    import addon_utils
    all_mods = addon_utils.modules()
    paper_addons = [m for m in all_mods if 'paper' in m.__name__.lower()]
    
    if paper_addons:
        print(f"✓ Found {len(paper_addons)} paper-related addon(s):")
        for addon in paper_addons:
            print(f"  - {addon.__name__}")
    else:
        print("✗ No paper model addons found")
        print("\nAvailable export addons:")
        export_addons = [m for m in all_mods if 'export' in m.__name__.lower() or 'io_' in m.__name__.lower()]
        for addon in export_addons[:10]:  # Show first 10
            print(f"  - {addon.__name__}")
except Exception as e:
    print(f"Error checking addons: {e}")

print("\n=== Installation Required ===")
print("The Export Paper Model addon must be manually installed:")
print("1. Download from: https://github.com/addam/Export-Paper-Model-from-Blender")
print("2. In Blender: Edit → Preferences → Add-ons → Install")
print("3. Select the downloaded ZIP file")
print("4. Enable the addon")
