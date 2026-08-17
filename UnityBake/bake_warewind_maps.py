# Bake Warewind procedural nodes to UV textures for Unity.
# Noise→ColorRamp→Roughness (all mats); ColorRamp→Bump→Normal (carboning).
# Usage:
#   blender.exe X-75-Warewind.blend --background --python bake_warewind_maps.py -- <out_dir>
import os
import sys
import bpy

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else None
if not OUT:
    raise SystemExit("need -- <out_dir>")
os.makedirs(OUT, exist_ok=True)

RES = 2048
MATS = (
    "GlossyBlackMetal",
    "MateBlackMetal",
    "MateWhiteMetal",
    "TexturedCarboningBlackMetal",
)


def find_node(nt, ntype):
    for n in nt.nodes:
        if n.type == ntype:
            return n
    return None


def ensure_uv(obj):
    me = obj.data
    if me.uv_layers:
        me.uv_layers.active = me.uv_layers[0]
        return
    # Fallback unwrap if FBX lost UV
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=66.0, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")


def objects_using_material(mat):
    out = []
    for obj in bpy.data.objects:
        if obj.type != "MESH" or obj.data is None:
            continue
        for slot in obj.material_slots:
            if slot.material == mat:
                out.append(obj)
                break
    return out


def bake_emit_to_png(objs, path, img_name):
    img = bpy.data.images.new(img_name, width=RES, height=RES, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    for obj in objs:
        ensure_uv(obj)
        for mat in (s.material for s in obj.material_slots if s.material):
            if not mat.node_tree:
                continue
            nodes = mat.node_tree.nodes
            for n in nodes:
                if n.type == "TEX_IMAGE":
                    n.select = False
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = img
            tex.select = True
            nodes.active = tex

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.context.scene.render.engine = "CYCLES"
    bpy.context.scene.cycles.samples = 16
    bpy.context.scene.cycles.bake_type = "EMIT"
    bpy.context.scene.render.bake.use_clear = True
    bpy.context.scene.render.bake.margin = 16
    bpy.ops.object.bake(type="EMIT")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def bake_normal_to_png(objs, path, img_name):
    img = bpy.data.images.new(img_name, width=RES, height=RES, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    for obj in objs:
        ensure_uv(obj)
        for mat in (s.material for s in obj.material_slots if s.material):
            if not mat.node_tree:
                continue
            nodes = mat.node_tree.nodes
            for n in nodes:
                if n.type == "TEX_IMAGE":
                    n.select = False
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = img
            tex.select = True
            nodes.active = tex

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.context.scene.render.engine = "CYCLES"
    bpy.context.scene.cycles.samples = 32
    bpy.context.scene.render.bake.normal_space = "TANGENT"
    bpy.context.scene.render.bake.use_clear = True
    bpy.context.scene.render.bake.margin = 16
    bpy.ops.object.bake(type="NORMAL")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def wire_emit_from_socket(mat, from_node, from_socket_name):
    """Temporarily force Principled Emission = socket, Strength=1 for EMIT bake."""
    nt = mat.node_tree
    bsdf = find_node(nt, "BSDF_PRINCIPLED")
    if bsdf is None:
        raise RuntimeError("no Principled on " + mat.name)
    # Clear emission links
    for link in list(nt.links):
        if link.to_node == bsdf and link.to_socket.name in ("Emission Color", "Emission Strength", "Emission"):
            nt.links.remove(link)
    emit_col = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
    emit_str = bsdf.inputs.get("Emission Strength")
    if emit_col is None:
        raise RuntimeError("no Emission socket on " + mat.name)
    nt.links.new(from_node.outputs[from_socket_name], emit_col)
    if emit_str is not None:
        emit_str.default_value = 1.0
    # Kill real surface contribution for cleaner emit (keep link to Output)
    return bsdf


def bake_roughness(mat_name):
    mat = bpy.data.materials.get(mat_name)
    if mat is None or mat.node_tree is None:
        print("SKIP missing", mat_name)
        return
    objs = objects_using_material(mat)
    if not objs:
        print("SKIP no objs", mat_name)
        return
    ramp = find_node(mat.node_tree, "VALTORGB")
    if ramp is None:
        print("SKIP no ramp", mat_name)
        return
    wire_emit_from_socket(mat, ramp, "Color")
    path = os.path.join(OUT, mat_name + "_Roughness.png")
    bake_emit_to_png(objs, path, mat_name + "_RoughnessBake")
    print("BAKED roughness", path)


def bake_carboning_normal():
    mat_name = "TexturedCarboningBlackMetal"
    mat = bpy.data.materials.get(mat_name)
    if mat is None or mat.node_tree is None:
        print("SKIP carboning")
        return
    objs = objects_using_material(mat)
    if not objs:
        print("SKIP carboning no objs")
        return
    # Keep full graph; Cycles NORMAL bake uses Principled Normal input (Bump).
    path = os.path.join(OUT, mat_name + "_Normal.png")
    bake_normal_to_png(objs, path, mat_name + "_NormalBake")
    print("BAKED normal", path)


# Mute ZenUV / other addons noise
bpy.context.scene.render.engine = "CYCLES"
bpy.context.scene.cycles.device = "CPU"

for name in MATS:
    bake_roughness(name)

bake_carboning_normal()
print("DONE", OUT)
