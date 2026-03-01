import bpy
import sys
import pathlib
import os
import zipfile
import traceback

# Legacy add-on bundling module name (folder inside the zip)
PAPER_MODEL_MODULE = "io_export_paper_model"


def get_command_line_args() -> dict:
    argv = sys.argv[sys.argv.index("--") + 1:]

    print("Command-line arguements:", argv)

    input_model = pathlib.Path(argv[argv.index("--input_model") + 1])
    output_model = pathlib.Path(argv[argv.index("--output_model") + 1])
    filename = argv[argv.index("--fn") + 1]
    doc_width = float(argv[argv.index("--dw") + 1])
    doc_height = float(argv[argv.index("--dh") + 1])
    ext = argv[argv.index("--ext") + 1]

    return {"input_model": input_model, "output_model": output_model,
            "fn": filename, "dw": doc_width, "dh": doc_height, "ext": ext}


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def install_and_enable_addon(addon_name: str):
    """
    Legacy add-on bundling (Option 1):
    - installs bundled zip (addon_name.zip) if needed
    - enables via bpy.ops.preferences.addon_enable(module=...)
    - saves user preferences so Blender keeps it enabled across runs

    Requirements:
    - zip must contain: <addon_name>/__init__.py at minimum
    - do not run Blender with --factory-startup (it ignores user prefs)
    """

    # If already enabled, just ensure prefs are saved and return
    if addon_name in bpy.context.preferences.addons:
        bpy.ops.wm.save_userpref()
        print(f"Addon '{addon_name}' already enabled.")
        return

    # Try enabling first (works if already installed but disabled)
    try:
        bpy.ops.preferences.addon_enable(module=addon_name)
        if addon_name in bpy.context.preferences.addons:
            bpy.ops.wm.save_userpref()
            print(f"Addon '{addon_name}' enabled.")
            return
    except Exception:
        # fall through to install path
        pass

    # Install from bundled legacy zip next to this script
    script_dir = os.path.dirname(os.path.realpath(__file__))
    zip_path = os.path.join(script_dir, f"{addon_name}.zip")

    if not os.path.exists(zip_path):
        print(f"Bundled add-on zip not found: {zip_path}")
        raise FileNotFoundError(zip_path)

    # Validate the legacy zip layout to avoid installing into an invalid folder name
    try:
        with zipfile.ZipFile(zip_path, "r") as z:
            expected_init = f"{addon_name}/__init__.py"
            if expected_init not in z.namelist():
                raise RuntimeError(
                    f"Invalid legacy zip layout. Expected '{expected_init}' inside {zip_path}"
                )
    except Exception as e:
        print(f"Failed validating add-on zip: {e}")
        raise

    print(f"Installing add-on '{addon_name}' from: {zip_path}")
    bpy.ops.preferences.addon_install(filepath=zip_path, overwrite=True)

    # Enable and save preferences
    try:
        bpy.ops.preferences.addon_enable(module=addon_name)
        bpy.ops.wm.save_userpref()
        print(f"Addon '{addon_name}' installed, enabled, and preferences saved.")
    except Exception as e:
        print(f"Failed to enable add-on '{addon_name}': {e}")
        traceback.print_exc()
        raise


def import_model(filepath: pathlib.Path):
    if filepath.exists():
        bpy.ops.wm.obj_import(filepath=str(filepath))

        # Make sure an imported mesh is active (operator context can depend on it)
        try:
            sel = [o for o in bpy.context.selected_objects if o.type == "MESH"]
            if sel:
                bpy.context.view_layer.objects.active = sel[0]
        except Exception:
            pass

        print(f"Model loaded and transformed: {filepath}")
    else:
        print(f"Model file not found: {filepath}")


def unfold(output_path: pathlib.Path):
    val = get_command_line_args()
    obj = bpy.context.object  # active imported mesh
    if obj is None:
        raise RuntimeError("No active object after import; cannot unfold/export.")
    obj.rotation_euler = (0, 0, 0)

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
    '''

    '''
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

    pm.output_size_x = 1
    pm.output_size_y = 1
    pm.use_auto_scale = False
    pm.limit_by_page = False
    pm.scale = 1

    '''Export'''

    filename = val['fn']
    export_file = str(output_path / filename)

    doc_width = val['dw']
    doc_height = val['dh']
    ext = val['ext']

    try:
        bpy.ops.export_mesh.paper_model(
            "EXEC_DEFAULT",
            filepath=export_file,
            page_size_preset='USER',
            output_size_x=doc_width,
            output_size_y=doc_height,
            output_margin=0,  # set to 0 since export team will handle the margins on the svg
            do_create_stickers=False,
            do_create_numbers=False,
            file_format=ext,
            scale=1
        )
    except Exception as e:
        # Optional retry signal for the C# side (if it's looking for "continue")
        msg = str(e).lower()
        if any(k in msg for k in ("fit", "does not fit", "doesn't fit", "page", "too large")):
            # Raise a RuntimeError so that "continue" appears on stderr for the C# side
            raise RuntimeError("continue") from e
        print(f"Failed to export paper model: {e}")
        traceback.print_exc()
        raise

    print(f"Exporting unfolded model to: {export_file}")


def main():
    paths = get_command_line_args()

    clear_scene()

    # Legacy add-on module name
    addon_name = PAPER_MODEL_MODULE
    install_and_enable_addon(addon_name)

    # Output directory from arguments (matches C# expectations)
    output_path = paths['output_model']
    output_path.mkdir(parents=True, exist_ok=True)

    import_model(paths['input_model'])

    unfold(output_path)


if __name__ == "__main__":
    main()
