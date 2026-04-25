import bpy # pyright: ignore[reportMissingImports]
import sys
import pathlib
import os
import bmesh # pyright: ignore[reportMissingImports]

'''
-------------------Important Notes-------------------
-Blender's python API can be updated which makes some methods no longer
 work properly. For example, Blender now has built-in support for obj import and 
 using old obj import extension on newer Blender builds can throw errors.
-paper model addon has LEGACY and EXTENSION--they are distinct addons. 
 Download from internet = bl_ext.blender_org.export_paper_model (extension)
-extension is newer, legacy packs islands less nicely
-In background mode, Blender does not save preference changes, so the addon
 is never persisted as "enabled" between runs.
-package_install already enables an addon in the current session--enabling again
 throws an error
'''

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
    full_module_name = f"bl_ext.blender_org.{addon_name}"

    # Disable legacy version if it's loaded to avoid conflicts
    if "io_export_paper_model" in bpy.context.preferences.addons:
        bpy.ops.preferences.addon_disable(module="io_export_paper_model")

    # Install if not present, else enable
    extension_path = os.path.join(
    bpy.utils.user_resource('EXTENSIONS'),
    "blender_org",  # not "user_default" — we confirmed it's blender_org
    addon_name
    )
    if not os.path.exists(extension_path):
        print(f"Addon '{addon_name}' not installed. Installing...")
        bpy.ops.extensions.package_install(repo_index=0, pkg_id=addon_name)
    elif full_module_name not in bpy.context.preferences.addons:
        # Only enable if not already active — avoids double-registration
        print(f"Enabling '{addon_name}'...")
        bpy.ops.preferences.addon_enable(module=full_module_name)
    else:
        print(f"Addon '{addon_name}' already active, skipping enable.")


def import_model(filepath: pathlib.Path):
    if filepath.exists():
        bpy.ops.wm.obj_import(filepath=str(filepath))
        print(f"Model loaded and transformed: {filepath}")
    else:
        print(f"Model file not found: {filepath}")

def cleanup():
    obj = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = obj
    obj.rotation_euler = (0, 0, 0)
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bmesh.update_edit_mesh(obj.data)
    bpy.ops.object.mode_set(mode='OBJECT')


def unfold(output_path: pathlib.Path, val: dict):
    # NOTE: doc_width and doc_height are in METERS (passed from .NET as meters)
    # Blender's paper model addon expects meters for output_size_x/y

    '''Context of Blender'''
    pm = bpy.context.scene.paper_model

    pm.use_auto_scale = False
    pm.limit_by_page = False
    pm.scale = 1

    '''Export'''
    filename = val['fn']
    export_file = str(output_path / filename)

    bpy.ops.export_mesh.paper_model(
        "EXEC_DEFAULT",
        filepath=export_file,
        page_size_preset='USER',
        output_size_x=val['dw'],
        output_size_y=val['dh'],
        output_margin=0, # set to 0 since export team will handle the margins on the svg
        file_format=val['ext'],
        scale=1
    )
    print(f"Exporting unfolded model to: {export_file}")
    

def main():
    paths = get_command_line_args()
    
    clear_scene() # might not be needed since every run is fresh, but we keep for now

    # 1) Install or enable
    addon_name = "export_paper_model"
    install_and_enable_addon(addon_name)

    # 2) Import model
    # NOTE: this might not be unfolding the modified file--just the imported one. Needs testing.
    output_path = paths["output_model"] # Use the output directory passed from the host app via --output_model.
    output_path.mkdir(parents=True, exist_ok=True)
    import_model(paths['input_model'])

    # 3) Cleanup: this is small changes to prevent crashes in edge cases
    cleanup()
    
    # 4) Unfold
    unfold(output_path, paths)

    # 5) Check if the size is empty and also where it went (debug)
    export_file = output_path / paths['fn']
    size = os.path.getsize(export_file)
    print(f"Output directory: {output_path}")
    print(f"Output file: {export_file} ({size} bytes)")
    if size == 0:
        raise RuntimeError("Export produced empty file!")


if __name__ == "__main__":
    main()
