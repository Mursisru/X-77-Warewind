# Dump Warewind blend: objects, materials, Principled sockets, textures, UV.
import json
import os
import sys
import bpy

out = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else None


def sock_info(s):
    info = {"name": s.name, "linked": s.is_linked}
    if hasattr(s, "default_value"):
        v = s.default_value
        try:
            info["value"] = [round(float(x), 4) for x in list(v)]
        except TypeError:
            try:
                info["value"] = round(float(v), 4)
            except Exception:
                info["value"] = str(v)
    if s.is_linked:
        link = s.links[0]
        info["from"] = link.from_node.name + "." + link.from_socket.name
        info["from_type"] = link.from_node.type
        if link.from_node.type == "TEX_IMAGE" and getattr(link.from_node, "image", None):
            img = link.from_node.image
            info["image"] = img.name
            info["image_path"] = img.filepath
            info["image_size"] = list(img.size) if img.size else None
    return info


def dump_mat(mat):
    d = {"name": mat.name, "use_nodes": mat.use_nodes, "blend": getattr(mat, "blend_method", None)}
    if not mat.node_tree:
        return d
    nodes = []
    for n in mat.node_tree.nodes:
        nd = {"name": n.name, "type": n.type, "label": n.label}
        if n.type == "TEX_IMAGE":
            nd["image"] = n.image.name if n.image else None
            nd["image_path"] = n.image.filepath if n.image else None
            nd["projection"] = getattr(n, "projection", None)
            nd["extension"] = getattr(n, "extension", None)
            if hasattr(n, "texture_mapping"):
                tm = n.texture_mapping
                nd["mapping"] = {
                    "loc": list(tm.translation),
                    "rot": list(tm.rotation),
                    "scale": list(tm.scale),
                }
        if n.type == "MAPPING":
            nd["vector_type"] = getattr(n, "vector_type", None)
            ins = {}
            for s in n.inputs:
                if hasattr(s, "default_value") and not s.is_linked:
                    try:
                        ins[s.name] = [round(float(x), 4) for x in list(s.default_value)]
                    except TypeError:
                        try:
                            ins[s.name] = round(float(s.default_value), 4)
                        except Exception:
                            pass
            nd["inputs"] = ins
        if n.type == "BSDF_PRINCIPLED":
            nd["inputs"] = [sock_info(s) for s in n.inputs if s.name in (
                "Base Color", "Metallic", "Roughness", "Specular IOR Level", "Specular",
                "Normal", "Alpha", "IOR", "Coat Weight", "Coat Roughness",
                "Emission Color", "Emission Strength", "Sheen Weight",
            ) or s.is_linked]
        nodes.append(nd)
    d["nodes"] = nodes
    return d


scene = {
    "objects": [],
    "materials": [],
    "images": [],
}
for obj in bpy.data.objects:
    item = {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "loc": [round(x, 5) for x in obj.location],
        "rot_e": [round(x, 5) for x in obj.rotation_euler],
        "scale": [round(x, 5) for x in obj.scale],
        "hide": obj.hide_get() if hasattr(obj, "hide_get") else False,
    }
    if obj.type == "MESH" and obj.data:
        me = obj.data
        item["verts"] = len(me.vertices)
        item["faces"] = len(me.polygons)
        item["uv"] = [uv.name for uv in me.uv_layers]
        item["mats"] = [s.material.name if s.material else None for s in obj.material_slots]
    if obj.type == "EMPTY":
        item["empty_type"] = obj.empty_display_type
        item["empty_size"] = obj.empty_display_size
    scene["objects"].append(item)

for mat in bpy.data.materials:
    if mat.users > 0:
        scene["materials"].append(dump_mat(mat))

for img in bpy.data.images:
    scene["images"].append({
        "name": img.name,
        "path": img.filepath,
        "size": list(img.size) if img.size else None,
        "packed": img.packed_file is not None,
        "users": img.users,
    })

text = json.dumps(scene, indent=2, ensure_ascii=False)
if out:
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        f.write(text)
    print("WROTE", out)
else:
    print(text)
print("OBJS", len(scene["objects"]), "MATS", len(scene["materials"]), "IMGS", len(scene["images"]))
