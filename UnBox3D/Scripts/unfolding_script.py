import bpy
import sys
import pathlib
import os

def get_command_line_args() -> dict:
    argv = sys.argv[sys.argv.index("--") + 1:]

    print("===== PYTHON SCRIPT STARTED =====")
    print(f"Command-line arguments: {argv}")

    input_model = pathlib.Path(argv[argv.index("--input_model") + 1])
    output_model = pathlib.Path(argv[argv.index("--output_model") + 1])
    filename = argv[argv.index("--fn") + 1]
    doc_width = float(argv[argv.index("--dw") + 1])
    doc_height = float(argv[argv.index("--dh") + 1])
    ext = argv[argv.index("--ext") + 1]

    # Optional: inverse scale factor to restore real-world dimensions
    inv_scale = 1.0
    if "--scale" in argv:
        inv_scale = float(argv[argv.index("--scale") + 1])

    print(f"Parsed arguments:")
    print(f"  Input model: {input_model}")
    print(f"  Output model: {output_model}")
    print(f"  Filename: {filename}")
    print(f"  Doc width: {doc_width}")
    print(f"  Doc height: {doc_height}")
    print(f"  Extension: {ext}")
    print(f"  Inverse scale: {inv_scale}")

    return {"input_model": input_model, "output_model": output_model,
            "fn": filename, "dw": doc_width, "dh": doc_height, "ext": ext,
            "inv_scale": inv_scale}


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    

def install_and_enable_addon(addon_name: str):
    """
    Enables the addon if it exists. 
    NOTE: The addon must be manually installed in Blender beforehand.
    Returns True if successful, False otherwise.
    """

    # Check if already enabled
    if addon_name in bpy.context.preferences.addons:
        print(f"✓ Addon '{addon_name}' is already enabled.")
        return True

    # Try to enable the addon
    print(f"Attempting to enable addon '{addon_name}'...")
    try:
        bpy.ops.preferences.addon_enable(module=addon_name)
        print(f"✓ Addon '{addon_name}' enabled successfully.")
        return True
    except Exception as e:
        print(f"✗ Could not enable addon '{addon_name}': {e}")
        return False


def import_model(filepath: pathlib.Path):
    print(f"Attempting to import model from: {filepath}")
    if filepath.exists():
        print(f"File exists, importing...")
        bpy.ops.wm.obj_import(filepath=str(filepath))

        obj = bpy.context.object
        if obj is None:
            print("ERROR: No object was imported")
            raise RuntimeError("No object was imported from the file")

        # Ensure the object is the active selection
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)

        # Triangulate the mesh to ensure all edges have proper angles
        # This fixes the "NoneType > int" bug in the Paper Model addon
        # which crashes on flat/simple geometry where edge.angle is None
        print(f"Triangulating mesh to ensure edge angle compatibility...")
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.quads_convert_to_tris()
        bpy.ops.object.mode_set(mode='OBJECT')

        print(f"Model loaded successfully: {filepath}")
    else:
        print(f"ERROR: Model file not found: {filepath}")
        raise FileNotFoundError(f"Model file not found: {filepath}")


def unfold(output_path: pathlib.Path):
    val = get_command_line_args()
    obj = bpy.context.object
    obj.rotation_euler = (0, 0, 0)

    # Apply inverse scale to restore real-world dimensions before unfolding
    inv_scale = val.get('inv_scale', 1.0)
    if inv_scale != 1.0:
        print(f"Applying inverse scale factor: {inv_scale} to restore real-world dimensions")
        obj.scale = (inv_scale, inv_scale, inv_scale)
        bpy.ops.object.transform_apply(scale=True)
        print(f"Model scaled to real-world size")

    '''
    Parameters

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

    -tabs and numbers display whether edge numbers and sticky tabs should appear
    -ext takes on SVG and PDF formats
    -output_size_x IS NOT the same as output_size_x for exporting
    its really weird, but the first output_size_x,output_size_y dictates the island limits in the 
    context of Blender and the second dictates the documents exported dimenions
    -scale seems to matter in the context of Blender and when exporting
    -page_size_preset seems irrelevant when exporting as its overrided when modifying the documents dimenions
    -use_auto_scale is in the context of Blender
    -limit_by_page is in the context of Blender
    
    units are in METERS! 8x4ft = 1.2x2.4m
    '''


    '''Context of Blender'''
    pm = bpy.context.scene.paper_model

    doc_width = val['dw']
    doc_height = val['dh']

    # Set the Blender-side page to match export size so all islands
    # are laid out on a single sheet instead of being split across pages
    pm.output_size_x = doc_width
    pm.output_size_y = doc_height
    pm.use_auto_scale = True
    pm.limit_by_page = True
    pm.scale = 1

    # Line styles to distinguish cut-through vs bevel-fold lines on the rotary cutter:
    #   outer  = SOLID  (thick black)  -> cut all the way through
    #   convex = DASH   (red dashed)   -> bevel 45° cut, mountain fold
    #   concave = DASHDOT (blue dash-dot) -> bevel 45° cut, valley fold
    # Different addon versions store the style in different places
    style = None

    # Try scene property group first (user_default version)
    if hasattr(pm, 'style'):
        style = pm.style

    # Try the operator's last-used settings (blender_org version)
    if style is None:
        try:
            op_class = bpy.ops.export_mesh.paper_model.get_rna_type()
            if hasattr(op_class, 'style'):
                style = op_class.style
        except Exception:
            pass

    if style is not None:
        try:
            style.line_width = 0.0002
            # Outer edges: solid black = cut through
            style.outer_color = (0, 0, 0, 1)
            style.outer_style = 'SOLID'
            style.outer_width = 3
            style.use_outbg = True
            style.outbg_color = (1, 1, 1, 1)
            style.outbg_width = 5
            # Convex edges: red dashed = mountain fold (bevel cut)
            style.convex_color = (0.8, 0, 0, 1)
            style.convex_style = 'DASH'
            style.convex_width = 2
            # Concave edges: blue dash-dot = valley fold (bevel cut)
            style.concave_color = (0, 0, 0.8, 1)
            style.concave_style = 'DASHDOT'
            style.concave_width = 2
            # Freestyle edges: solid gray
            style.freestyle_color = (0.5, 0.5, 0.5, 1)
            style.freestyle_style = 'SOLID'
            style.freestyle_width = 2
            # Background / inner
            style.use_inbg = True
            style.inbg_color = (1, 1, 1, 1)
            style.inbg_width = 2
            # Stickers / text
            style.sticker_color = (0.9, 0.9, 0.9, 1)
            style.text_color = (0, 0, 0, 1)
            print("Style configured: SOLID=cut-through, DASH(red)=mountain fold, DASHDOT(blue)=valley fold")
        except Exception as e:
            print(f"Warning: Could not set all style properties: {e}")
            print("Exporting with default line styles")
    else:
        print("Note: Style property not found on this addon version, using default line styles")

    '''Export'''

    filename = val['fn']
    export_file = str(output_path / filename)

    ext = val['ext']

    # Build export kwargs — some addon versions use different parameter names
    export_kwargs = {
        "filepath": export_file,
        "page_size_preset": "USER",
        "output_size_x": doc_width,
        "output_size_y": doc_height,
        "output_margin": 0,
        "file_format": ext,
        "scale": 1,
    }

    # Detect supported operator properties to handle different addon versions
    op_props = bpy.ops.export_mesh.paper_model.get_rna_type().properties.keys()
    if 'do_create_stickers' in op_props:
        export_kwargs['do_create_stickers'] = False
    if 'do_create_numbers' in op_props:
        export_kwargs['do_create_numbers'] = False

    # Attempt export, retrying with larger page sizes if an island doesn't fit
    max_retries = 5
    for attempt in range(max_retries + 1):
        try:
            bpy.ops.export_mesh.paper_model("EXEC_DEFAULT", **export_kwargs)
            break
        except RuntimeError as e:
            if "island is too big" in str(e) and attempt < max_retries:
                export_kwargs["output_size_x"] *= 2
                export_kwargs["output_size_y"] *= 2
                print(f"Island too big for page, retrying with "
                      f"{export_kwargs['output_size_x']}x{export_kwargs['output_size_y']} "
                      f"(attempt {attempt + 2}/{max_retries + 1})")
            else:
                raise

    print(f"Successfully exported unfolded model to: {export_file}")
    print(f"File format: {ext}")
    print("===== PYTHON SCRIPT COMPLETED SUCCESSFULLY =====")
    

def main():
    paths = get_command_line_args()

    clear_scene()

    # Try multiple possible addon names (Blender 4.2 uses extensions system)
    possible_addon_names = [
        "bl_ext.user_default.export_paper_model",
        "bl_ext.blender_org.export_paper_model",
        "export_paper_model"
    ]

    addon_enabled = False
    for addon_name in possible_addon_names:
        result = install_and_enable_addon(addon_name)
        if result:
            addon_enabled = True
            print(f"✓ Using addon: {addon_name}")
            break

    if not addon_enabled:
        print("✗ ERROR: Could not enable Export Paper Model addon")
        print("Please ensure the addon is installed in Blender:")
        print("1. Open Blender 4.2")
        print("2. Edit → Preferences → Add-ons")
        print("3. Search for 'paper' and enable it")
        return

    # Use the output path passed from C# instead of hardcoded Downloads folder
    output_path = paths['output_model']

    # Ensure output directory exists
    output_path.mkdir(parents=True, exist_ok=True)

    print(f"Output directory set to: {output_path}")

    import_model(paths['input_model'])

    # Pass the correct output path
    unfold(output_path)


if __name__ == "__main__":
    main()

