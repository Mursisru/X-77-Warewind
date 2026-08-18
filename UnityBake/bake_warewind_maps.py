# Bake Warewind Principled graphs to UV textures + look.json for Unity.
# Face-isolated per material so overlapping 0-1 UVs on other slots cannot overwrite.
# Does not change authored active/render UV.
# blender.exe X-75-Warewind.blend --background --python bake_warewind_maps.py -- <out_dir>
import json
import os
import sys
import bpy
import bmesh

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else None
if not OUT:
    raise SystemExit("need -- <out_dir>")
os.makedirs(OUT, exist_ok=True)

RES = 2048


def find_bsdf(nt):
    for n in nt.nodes:
        if n.type == "BSDF_PRINCIPLED":
            return n
    return None


def sock_value(s):
    if s is None:
        return None
    v = s.default_value
    try:
        return [round(float(x), 5) for x in list(v)]
    except TypeError:
        try:
            return round(float(v), 5)
        except Exception:
            return None


def ensure_uv(obj):
    me = obj.data
    if me.uv_layers:
        return
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


def isolate_faces(obj, mat):
    idxs = {i for i, s in enumerate(obj.material_slots) if s.material == mat}
    if not idxs:
        return None
    dup = obj.copy()
    dup.data = obj.data.copy()
    dup.name = obj.name + "__bake"
    bpy.context.scene.collection.objects.link(dup)
    bm = bmesh.new()
    bm.from_mesh(dup.data)
    drop = [f for f in bm.faces if f.material_index not in idxs]
    if drop:
        bmesh.ops.delete(bm, geom=drop, context="FACES")
    bm.to_mesh(dup.data)
    bm.free()
    if len(dup.data.polygons) == 0:
        me = dup.data
        bpy.data.objects.remove(dup, do_unlink=True)
        bpy.data.meshes.remove(me)
        return None
    dup.data.materials.clear()
    dup.data.materials.append(mat)
    for p in dup.data.polygons:
        p.material_index = 0
    ensure_uv(dup)
    return dup


def free_temps(temps):
    for t in temps:
        if t is None:
            continue
        me = t.data
        bpy.data.objects.remove(t, do_unlink=True)
        if me is not None:
            bpy.data.meshes.remove(me)


def wire_emit(mat, from_node, from_socket_name):
    nt = mat.node_tree
    bsdf = find_bsdf(nt)
    if bsdf is None:
        raise RuntimeError("no Principled on " + mat.name)
    for link in list(nt.links):
        if link.to_node == bsdf and link.to_socket.name in ("Emission Color", "Emission Strength", "Emission"):
            nt.links.remove(link)
    emit_col = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
    emit_str = bsdf.inputs.get("Emission Strength")
    src = from_node.outputs.get(from_socket_name)
    if emit_col is None or src is None:
        raise RuntimeError("emit wire fail " + mat.name)
    nt.links.new(src, emit_col)
    if emit_str is not None:
        emit_str.default_value = 1.0


def add_bake_target(mat, img):
    nodes = mat.node_tree.nodes
    for n in nodes:
        if n.type == "TEX_IMAGE":
            n.select = False
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.select = True
    nodes.active = tex
    return tex


def drop_node(mat, node):
    if node is not None:
        mat.node_tree.nodes.remove(node)


def bake_emit(temps, mat, path, img_name):
    img = bpy.data.images.new(img_name, width=RES, height=RES, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    tex = add_bake_target(mat, img)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in temps:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = temps[0]
    bpy.context.scene.render.engine = "CYCLES"
    bpy.context.scene.cycles.samples = 32
    bpy.context.scene.cycles.bake_type = "EMIT"
    bpy.context.scene.render.bake.use_clear = True
    bpy.context.scene.render.bake.margin = 16
    bpy.ops.object.bake(type="EMIT")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    drop_node(mat, tex)
    bpy.data.images.remove(img)


def bake_normal(temps, mat, path, img_name):
    img = bpy.data.images.new(img_name, width=RES, height=RES, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    tex = add_bake_target(mat, img)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in temps:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = temps[0]
    bpy.context.scene.render.engine = "CYCLES"
    bpy.context.scene.cycles.samples = 32
    bpy.context.scene.render.bake.normal_space = "TANGENT"
    bpy.context.scene.render.bake.use_clear = True
    bpy.context.scene.render.bake.margin = 16
    bpy.ops.object.bake(type="NORMAL")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    drop_node(mat, tex)
    bpy.data.images.remove(img)


def unpack_images():
    for img in bpy.data.images:
        if img.packed_file is None or not img.size[0]:
            continue
        if img.name in ("Render Result", "Viewer Node"):
            continue
        stem = os.path.splitext(os.path.basename(img.name))[0]
        ext = os.path.splitext(img.name)[1] or ".png"
        path = os.path.join(OUT, stem + ext)
        img.filepath_raw = path
        img.file_format = "JPEG" if ext.lower() in (".jpg", ".jpeg") else "PNG"
        img.save()
        print("UNPACKED", path)


def dump_look():
    mats = []
    for mat in bpy.data.materials:
        if mat.users <= 0 or mat.node_tree is None:
            continue
        bsdf = find_bsdf(mat.node_tree)
        if bsdf is None:
            continue
        ins = bsdf.inputs
        bc = sock_value(ins.get("Base Color")) or [1, 1, 1, 1]
        if not isinstance(bc, list):
            bc = [bc, bc, bc, 1.0]
        while len(bc) < 4:
            bc.append(1.0)
        rough = sock_value(ins.get("Roughness"))
        if isinstance(rough, list):
            rough = rough[0]
        met = sock_value(ins.get("Metallic"))
        if isinstance(met, list):
            met = met[0]
        spec = sock_value(ins.get("Specular IOR Level") or ins.get("Specular"))
        if isinstance(spec, list):
            spec = spec[0]
        ior = sock_value(ins.get("IOR"))
        if isinstance(ior, list):
            ior = ior[0]
        alpha = sock_value(ins.get("Alpha"))
        if isinstance(alpha, list):
            alpha = alpha[0]
        mats.append({
            "name": mat.name,
            "colR": bc[0],
            "colG": bc[1],
            "colB": bc[2],
            "colA": bc[3],
            "metallic": met if met is not None else 0.0,
            "roughness": rough if rough is not None else 0.5,
            "specular": spec if spec is not None else 0.5,
            "ior": ior if ior is not None else 1.5,
            "alpha": alpha if alpha is not None else 1.0,
            "baseColorLinked": 1 if (ins.get("Base Color") and ins["Base Color"].is_linked) else 0,
            "roughnessLinked": 1 if (ins.get("Roughness") and ins["Roughness"].is_linked) else 0,
            "normalLinked": 1 if (ins.get("Normal") and ins["Normal"].is_linked) else 0,
            "blend": getattr(mat, "blend_method", "OPAQUE") or "OPAQUE",
        })
    path = os.path.join(OUT, "warewind_look.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump({"mats": mats}, f, indent=2)
    print("LOOK", path, "n", len(mats))
    return {m["name"]: m for m in mats}


def log_uvs():
    for obj in bpy.data.objects:
        if obj.type != "MESH" or obj.data is None or not obj.data.uv_layers:
            continue
        me = obj.data
        active = me.uv_layers.active.name if me.uv_layers.active else None
        render = None
        for uv in me.uv_layers:
            if getattr(uv, "active_render", False):
                render = uv.name
        print("UV", obj.name, "layers", [uv.name for uv in me.uv_layers],
              "active", active, "render", render)


bpy.context.scene.render.engine = "CYCLES"
bpy.context.scene.cycles.device = "CPU"

log_uvs()
unpack_images()
look = dump_look()

for mat in list(bpy.data.materials):
    if mat.users <= 0 or mat.node_tree is None:
        continue
    bsdf = find_bsdf(mat.node_tree)
    if bsdf is None:
        continue
    src_objs = objects_using_material(mat)
    if not src_objs:
        print("SKIP no objs", mat.name)
        continue
    temps = []
    for o in src_objs:
        iso = isolate_faces(o, mat)
        if iso is not None:
            temps.append(iso)
    if not temps:
        print("SKIP empty isolate", mat.name)
        continue

    try:
        rough = bsdf.inputs.get("Roughness")
        if rough is not None and rough.is_linked:
            ln = rough.links[0]
            wire_emit(mat, ln.from_node, ln.from_socket.name)
            path = os.path.join(OUT, mat.name + "_Roughness.png")
            bake_emit(temps, mat, path, mat.name + "_RoughnessBake")
            print("BAKED roughness", path)

        nrm = bsdf.inputs.get("Normal")
        if nrm is not None and nrm.is_linked:
            path = os.path.join(OUT, mat.name + "_Normal.png")
            bake_normal(temps, mat, path, mat.name + "_NormalBake")
            print("BAKED normal", path)
        elif not (rough is not None and rough.is_linked):
            print("SKIP maps (scalar only)", mat.name)
    finally:
        free_temps(temps)

print("DONE", OUT)
