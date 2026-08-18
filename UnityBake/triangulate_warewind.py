# Triangulate + export Warewind FBX 1:1 (empties, speakers→empties, authored UV).
# Do not embed images — grunge is a roughness mixer, not albedo.
# blender.exe <blend> --background --python triangulate_warewind.py -- <out.fbx>
import sys
import bpy
import bmesh

argv = sys.argv
out_fbx = argv[argv.index("--") + 1]

# Speakers do not survive FBX; keep world TRS as Empty with the same name.
for obj in list(bpy.data.objects):
    if obj.type != "SPEAKER":
        continue
    name = obj.name
    mw = obj.matrix_world.copy()
    parent = obj.parent
    parent_inv = obj.matrix_parent_inverse.copy() if parent else None
    empty = bpy.data.objects.new(name + "__tmpEmpty", None)
    empty.empty_display_type = "SINGLE_ARROW"
    empty.empty_display_size = getattr(obj, "empty_display_size", 0.4) or 0.4
    bpy.context.scene.collection.objects.link(empty)
    if parent:
        empty.parent = parent
        empty.matrix_parent_inverse = parent_inv
    empty.matrix_world = mw
    obj.name = name + "_SpeakerSrc"
    empty.name = name
    print("SPEAKER_TO_EMPTY", name, "loc", list(empty.location), "rot", list(empty.rotation_euler))

for obj in list(bpy.data.objects):
    if obj.type != "MESH" or obj.data is None:
        continue
    me = obj.data
    uv_name = me.uv_layers.active.name if me.uv_layers else None
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(
        bm,
        faces=bm.faces[:],
        quad_method="BEAUTY",
        ngon_method="BEAUTY",
    )
    bm.to_mesh(me)
    bm.free()
    me.update()
    print("TRIANGULATED", obj.name, "faces", len(me.polygons), "verts", len(me.vertices), "uv", uv_name)
    print("  slots", [s.material.name if s.material else None for s in obj.material_slots])

kwargs = dict(
    filepath=out_fbx,
    use_selection=False,
    object_types={"MESH", "EMPTY"},
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_tspace=True,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="STRIP",
    embed_textures=False,
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_NONE",
    bake_space_transform=False,
    use_custom_props=True,
)
try:
    bpy.ops.export_scene.fbx(use_triangles=True, **kwargs)
except TypeError:
    bpy.ops.export_scene.fbx(**kwargs)
print("EXPORTED", out_fbx)
