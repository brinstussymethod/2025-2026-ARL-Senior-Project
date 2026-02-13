import bpy       # Blender Python API — controls Blender's scene, objects, operators
import sys       # System module — used to read command-line arguments passed to Blender
import pathlib   # Path handling — cross-platform file path manipulation
import os        # OS utilities — file/directory operations

def get_command_line_args() -> dict:
    """Parses custom arguments passed after '--' on the Blender command line."""
    # Blender puts its own args before '--', our custom args come after it
    argv = sys.argv[sys.argv.index("--") + 1:]

    print("===== PYTHON SCRIPT STARTED =====")
    print(f"Command-line arguments: {argv}")

    # Extract each named argument by finding its flag and reading the next value
    input_model = pathlib.Path(argv[argv.index("--input_model") + 1])   # Path to the .obj file to unfold
    output_model = pathlib.Path(argv[argv.index("--output_model") + 1]) # Directory where SVG/PDF output goes
    filename = argv[argv.index("--fn") + 1]                             # Output filename (without extension)
    doc_width = float(argv[argv.index("--dw") + 1])                     # Page width in meters for the SVG
    doc_height = float(argv[argv.index("--dh") + 1])                    # Page height in meters for the SVG
    ext = argv[argv.index("--ext") + 1]                                 # File format: "SVG" or "PDF"

    # Optional: inverse scale factor (e.g., 7.0 means the model was scaled down 7x in the app)
    # Multiplying by this restores the real-world size before unfolding
    inv_scale = 1.0
    if "--scale" in argv:
        inv_scale = float(argv[argv.index("--scale") + 1])

    # Log all parsed values for debugging
    print(f"Parsed arguments:")
    print(f"  Input model: {input_model}")
    print(f"  Output model: {output_model}")
    print(f"  Filename: {filename}")
    print(f"  Doc width: {doc_width}")
    print(f"  Doc height: {doc_height}")
    print(f"  Extension: {ext}")
    print(f"  Inverse scale: {inv_scale}")

    # Return all parsed args as a dictionary for use by other functions
    return {"input_model": input_model, "output_model": output_model,
            "fn": filename, "dw": doc_width, "dh": doc_height, "ext": ext,
            "inv_scale": inv_scale}


def clear_scene():
    """Removes all objects from the Blender scene so we start with a clean slate."""
    bpy.ops.object.select_all(action='SELECT')   # Select every object in the scene
    bpy.ops.object.delete(use_global=False)       # Delete all selected objects


def install_and_enable_addon(addon_name: str):
    """
    Enables the Export Paper Model addon by its module name.
    The addon must already be installed in Blender (via Extensions or manually).
    Returns True if the addon is now enabled, False if it couldn't be enabled.
    """

    # Check if the addon is already enabled — no work needed
    if addon_name in bpy.context.preferences.addons:
        print(f"✓ Addon '{addon_name}' is already enabled.")
        return True

    # Try to enable the addon using Blender's preferences operator
    print(f"Attempting to enable addon '{addon_name}'...")
    try:
        bpy.ops.preferences.addon_enable(module=addon_name)  # Activate the addon
        print(f"✓ Addon '{addon_name}' enabled successfully.")
        return True
    except Exception as e:
        # This will fail if the addon isn't installed under this module name
        print(f"✗ Could not enable addon '{addon_name}': {e}")
        return False


def import_model(filepath: pathlib.Path):
    """Imports an .obj file into Blender, selects it, and triangulates the mesh."""
    print(f"Attempting to import model from: {filepath}")
    if filepath.exists():
        print(f"File exists, importing...")
        bpy.ops.wm.obj_import(filepath=str(filepath))  # Import the .obj file into the scene

        obj = bpy.context.object  # Get the newly imported object (Blender sets it as active)
        if obj is None:
            print("ERROR: No object was imported")
            raise RuntimeError("No object was imported from the file")

        # Make sure this object is selected and active (required for edit mode operations)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)

        # Triangulate all faces to ensure every edge has a valid angle.
        # The Paper Model addon crashes with "NoneType > int" on flat/coplanar faces
        # where edge.angle returns None. Triangulation guarantees all edges have angles.
        print(f"Triangulating mesh to ensure edge angle compatibility...")
        bpy.ops.object.mode_set(mode='EDIT')           # Switch to edit mode to modify geometry
        bpy.ops.mesh.select_all(action='SELECT')       # Select all faces/edges/vertices
        bpy.ops.mesh.quads_convert_to_tris()            # Convert all quads to triangles
        bpy.ops.object.mode_set(mode='OBJECT')          # Switch back to object mode

        print(f"Model loaded successfully: {filepath}")
    else:
        print(f"ERROR: Model file not found: {filepath}")
        raise FileNotFoundError(f"Model file not found: {filepath}")


def unfold(output_path: pathlib.Path):
    """Unfolds the active mesh object into a flat paper model and exports it as SVG/PDF."""
    val = get_command_line_args()          # Re-parse args to get export settings
    obj = bpy.context.object               # Get the currently active (imported) object
    obj.rotation_euler = (0, 0, 0)         # Reset rotation so the unfold starts from a clean orientation

    # Apply inverse scale to restore real-world dimensions before unfolding.
    # The C# app scales the model down for viewport display (e.g., 7x smaller).
    # We multiply by that factor here to get the actual physical dimensions.
    inv_scale = val.get('inv_scale', 1.0)
    if inv_scale != 1.0:
        print(f"Applying inverse scale factor: {inv_scale} to restore real-world dimensions")
        obj.scale = (inv_scale, inv_scale, inv_scale)        # Scale the object uniformly
        bpy.ops.object.transform_apply(scale=True)           # Bake the scale into the mesh data
        print(f"Model scaled to real-world size")

    '''
    Reference: Full parameter list for the Export Paper Model operator
    (documented here for developer reference — not all are used)

    bpy.ops.export_mesh.paper_model(
    filepath="", filename="", directory="", 
    page_size_preset='A4', output_size_x=0.21, output_size_y=0.297, output_margin=0.005, output_type='NONE', 
    do_create_stickers=True, do_create_numbers=True, sticker_width=0.005, angle_epsilon=0.00872665, 
    output_dpi=90, bake_samples=64, file_format='PDF', image_packing='ISLAND_EMBED', scale=1, 
    do_create_uvmap=False, ui_expanded_document=True, ui_expanded_style=False, 
    style={"name":"", "outer_color":(0, 0, 0, 1), "outer_style":'SOLID', 
    "line_width":0.0001, "outer_width":3, "use_outbg":True, "outbg_color":(1, 1, 1, 1), "outbg_width":5, 
    "convex_color":(0, 0, 0, 1), "convex_style":'DASH', "convex_width":2, "concave_color":(0, 0, 0, 1), 
    "concave_style":'DASHDOT', "concave_width":2, "freestyle_color":(0, 0, 0, 1), "freestyle_style":'SOLID', 
    "freestyle_width":2, "use_inbg":True, "inbg_color":(1, 1, 1, 1), "inbg_width":2, 
    "sticker_color":(0.9, 0.9, 0.9, 1), "text_color":(0, 0, 0, 1)})

    Notes:
    - do_create_stickers/do_create_numbers: toggle glue tab labels (not all addon versions have these)
    - ext: file format — "SVG" or "PDF"
    - output_size_x/y in Blender context: controls island layout area (how big the "canvas" is)
    - output_size_x/y in export kwargs: controls the actual exported document dimensions
    - scale: model scale factor (1 = real size)
    - page_size_preset: ignored when we set USER and specify custom dimensions
    - use_auto_scale: Blender-side — auto-shrinks model to fit the page
    - limit_by_page: Blender-side — prevents islands from overflowing the page
    - All dimensions are in METERS (8ft x 4ft = 2.4m x 1.2m)
    '''


    # ==================== BLENDER CONTEXT SETUP ====================
    # Access the Paper Model addon's scene-level settings
    pm = bpy.context.scene.paper_model

    doc_width = val['dw']    # Export page width in meters (passed from C#)
    doc_height = val['dh']   # Export page height in meters (passed from C#)

    # Configure Blender's internal layout page to match the export dimensions.
    # This ensures all unfolded islands are arranged on ONE page, not split across many.
    pm.output_size_x = doc_width     # Set the island layout canvas width
    pm.output_size_y = doc_height    # Set the island layout canvas height
    pm.use_auto_scale = True         # Auto-shrink model to fit within the page
    pm.limit_by_page = True          # Prevent islands from overflowing the page boundary
    pm.scale = 1                     # Use 1:1 scale (real-world dimensions)

    # ==================== LINE STYLE CONFIGURATION ====================
    # Configure different line styles so the rotary cutter operator knows what to do:
    #   SOLID lines (black)     = cut all the way through the cardboard
    #   DASH lines (red)        = bevel 45° cut for mountain fold (convex edge)
    #   DASHDOT lines (blue)    = bevel 45° cut for valley fold (concave edge)
    #
    # The style property location varies by addon version:
    #   - user_default version: pm.style (scene property group)
    #   - blender_org version: doesn't have pm.style (uses operator defaults)
    style = None

    # Attempt 1: Try scene property group (works on user_default addon version)
    if hasattr(pm, 'style'):
        style = pm.style

    # Attempt 2: Try the operator's RNA type (works on some blender_org versions)
    if style is None:
        try:
            op_class = bpy.ops.export_mesh.paper_model.get_rna_type()  # Get operator's registered type
            if hasattr(op_class, 'style'):
                style = op_class.style
        except Exception:
            pass  # Silently continue — we'll use defaults

    # If we found a style object, configure all line properties
    if style is not None:
        try:
            style.line_width = 0.0002              # Base line width in meters (0.2mm)
            # Outer edges: solid black lines = CUT ALL THE WAY THROUGH
            style.outer_color = (0, 0, 0, 1)       # Black color (RGBA)
            style.outer_style = 'SOLID'             # Solid line = full cut
            style.outer_width = 3                   # 3x base width (thick)
            style.use_outbg = True                  # Draw a white background behind outer lines
            style.outbg_color = (1, 1, 1, 1)        # White background color
            style.outbg_width = 5                   # Background slightly wider than the line
            # Convex edges: red dashed lines = MOUNTAIN FOLD (bevel 45° cut)
            style.convex_color = (0.8, 0, 0, 1)     # Red color
            style.convex_style = 'DASH'              # Dashed line = fold here
            style.convex_width = 2                   # 2x base width
            # Concave edges: blue dash-dot lines = VALLEY FOLD (bevel 45° cut)
            style.concave_color = (0, 0, 0.8, 1)    # Blue color
            style.concave_style = 'DASHDOT'          # Dash-dot line = fold here (other direction)
            style.concave_width = 2                  # 2x base width
            # Freestyle edges: solid gray lines (decorative/internal detail lines)
            style.freestyle_color = (0.5, 0.5, 0.5, 1)  # Gray color
            style.freestyle_style = 'SOLID'              # Solid line
            style.freestyle_width = 2                    # 2x base width
            # Inner line background: white behind fold lines for visibility
            style.use_inbg = True                    # Enable background on inner lines
            style.inbg_color = (1, 1, 1, 1)          # White background
            style.inbg_width = 2                     # Background width
            # Sticker/tab and text colors (tabs are disabled, but values still needed)
            style.sticker_color = (0.9, 0.9, 0.9, 1)  # Light gray for glue tabs
            style.text_color = (0, 0, 0, 1)            # Black text
            print("Style configured: SOLID=cut-through, DASH(red)=mountain fold, DASHDOT(blue)=valley fold")
        except Exception as e:
            # Some addon versions may not support all properties — export with defaults
            print(f"Warning: Could not set all style properties: {e}")
            print("Exporting with default line styles")
    else:
        # blender_org version doesn't expose style — use addon's built-in defaults
        print("Note: Style property not found on this addon version, using default line styles")

    # ==================== EXPORT ====================
    filename = val['fn']                           # Output filename without extension
    export_file = str(output_path / filename)      # Full export path (addon appends the extension)

    ext = val['ext']                               # File format: "SVG" or "PDF"

    # Build the keyword arguments for the export operator
    export_kwargs = {
        "filepath": export_file,         # Where to save the file
        "page_size_preset": "USER",      # Use custom page dimensions (not A4/Letter)
        "output_size_x": doc_width,      # Exported document width in meters
        "output_size_y": doc_height,     # Exported document height in meters
        "output_margin": 0,              # No margin around the page edges
        "file_format": ext,              # "SVG" or "PDF"
        "scale": 1,                      # 1:1 scale (real-world size)
    }

    # Check which optional parameters this addon version supports.
    # The user_default version has do_create_stickers/do_create_numbers;
    # the blender_org version may not. We introspect the operator to avoid TypeError.
    op_props = bpy.ops.export_mesh.paper_model.get_rna_type().properties.keys()  # All supported params
    if 'do_create_stickers' in op_props:
        export_kwargs['do_create_stickers'] = False   # Disable glue tab generation
    if 'do_create_numbers' in op_props:
        export_kwargs['do_create_numbers'] = False    # Disable edge number labels

    # Run the export operator with retry logic for oversized islands.
    # If an unfolded island is too large for the page, double the page size and retry.
    max_retries = 5                                    # Maximum number of retry attempts
    for attempt in range(max_retries + 1):
        try:
            bpy.ops.export_mesh.paper_model("EXEC_DEFAULT", **export_kwargs)  # Execute the export
            break                                      # Success — exit the retry loop
        except RuntimeError as e:
            if "island is too big" in str(e) and attempt < max_retries:
                # Island doesn't fit — double the page dimensions and try again
                export_kwargs["output_size_x"] *= 2
                export_kwargs["output_size_y"] *= 2
                print(f"Island too big for page, retrying with "
                      f"{export_kwargs['output_size_x']}x{export_kwargs['output_size_y']} "
                      f"(attempt {attempt + 2}/{max_retries + 1})")
            else:
                raise                                  # Out of retries or different error — crash

    print(f"Successfully exported unfolded model to: {export_file}")
    print(f"File format: {ext}")
    print("===== PYTHON SCRIPT COMPLETED SUCCESSFULLY =====")
    

def main():
    """Entry point: clears scene, enables addon, imports model, unfolds, and exports."""
    paths = get_command_line_args()   # Parse all command-line arguments

    clear_scene()                      # Remove all default objects from the Blender scene

    # The addon module name varies depending on how it was installed:
    #   - "bl_ext.user_default.export_paper_model" = installed via Blender Extensions (user)
    #   - "bl_ext.blender_org.export_paper_model"  = installed via Blender Extensions (official)
    #   - "export_paper_model"                     = installed manually into scripts/addons
    possible_addon_names = [
        "bl_ext.user_default.export_paper_model",
        "bl_ext.blender_org.export_paper_model",
        "export_paper_model"
    ]

    # Try each addon name until one works
    addon_enabled = False
    for addon_name in possible_addon_names:
        result = install_and_enable_addon(addon_name)   # Try to enable this addon name
        if result:
            addon_enabled = True
            print(f"✓ Using addon: {addon_name}")
            break                                        # Found a working addon — stop trying

    # If none of the addon names worked, print instructions and exit
    if not addon_enabled:
        print("✗ ERROR: Could not enable Export Paper Model addon")
        print("Please ensure the addon is installed in Blender:")
        print("1. Open Blender 4.2")
        print("2. Edit → Preferences → Add-ons")
        print("3. Search for 'paper' and enable it")
        return

    # Get the output directory path (passed from C# MainViewModel)
    output_path = paths['output_model']

    # Create the output directory if it doesn't exist yet
    output_path.mkdir(parents=True, exist_ok=True)

    print(f"Output directory set to: {output_path}")

    # Import the .obj model file into Blender
    import_model(paths['input_model'])

    # Unfold the model into a flat paper pattern and export as SVG/PDF
    unfold(output_path)


# Standard Python entry point — only runs when script is executed directly
if __name__ == "__main__":
    main()

