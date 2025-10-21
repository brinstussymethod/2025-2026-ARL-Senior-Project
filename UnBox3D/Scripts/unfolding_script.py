import bpy
import sys
import pathlib
import os

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

    # If already enabled, do nothing
    if addon_name in bpy.context.preferences.addons:
        print(f"Addon '{addon_name}' already enabled.")
        return
    
    # If addon not installed, install it
    addon_path = os.path.join(bpy.utils.user_resource('SCRIPTS', "addons"), addon_name)
    if not os.path.exists(addon_path):
        print(f"Addon '{addon_name}' not installed. Installing...")
        bpy.ops.extensions.package_install(repo_index=0, pkg_id=addon_name)

    # Enable the addon
    try:
        bpy.ops.preferences.addon_enable(module=addon_name)
        print(f"Addon '{addon_name}' installed and enabled.")
    except Exception as e:
        print(f"Failed to enable add-on '{addon_name}': {e}")


def import_model(filepath: pathlib.Path):
    if filepath.exists():
        bpy.ops.wm.obj_import(filepath=str(filepath))

        print(f"Model loaded and transformed: {filepath}")
    else:
        print(f"Model file not found: {filepath}")


def unfold(output_path: pathlib.Path):
    val = get_command_line_args()
    obj = bpy.context.object #might delete 
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

    pm.output_size_x=1
    pm.output_size_y=1
    pm.use_auto_scale = False
    pm.limit_by_page = False
    pm.scale = 1

    '''Export'''

    filename = val['fn']
    export_file = str(output_path / filename)

    doc_width = val['dw']
    doc_height = val['dh']
    ext = val['ext']

    bpy.ops.export_mesh.paper_model(
        "EXEC_DEFAULT",
        filepath=export_file,
        page_size_preset='USER',
        output_size_x=doc_width,
        output_size_y=doc_height,
        output_margin=0, # set to 0 since export team will handle the margins on the svg
        do_create_stickers=False,
        do_create_numbers=False,
        file_format=ext,
        scale=1
    )

    print(f"Exporting unfolded model to: {export_file}")
    

def main():
    paths = get_command_line_args()
    
    clear_scene()
    addon_name = "export_paper_model"
    install_and_enable_addon(addon_name)
    if addon_name in bpy.context.preferences.addons:
        bpy.ops.preferences.addon_enable(module=addon_name)
    else:
        print(f"Failed to install or enable addon '{addon_name}'. Exiting.")
        return

    downloads_path = pathlib.Path.home() / "Downloads"
    downloads_path.mkdir(parents=True, exist_ok=True)  # ensure it exists
    
    import_model(paths['input_model'])
    
    # Pass Downloads folder as output_path (for now)
    unfold(downloads_path)


if __name__ == "__main__":
    main()

