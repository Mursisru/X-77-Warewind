# Blender 5.2 CLI: triangulate Warewind meshes so Unity does not discard n-gons.
# blender.exe <blend> --background --python triangulate_warewind.py -- <out.fbx>
import sys
import bpy
import bmesh

argv = sys.argv
out_fbx = argv[argv.index("--") + 1]

for obj in list(bpy.data.objects):
    if obj.type != "MESH" or obj.data is None:
        continue
    me = obj.data
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
    print("TRIANGULATED", obj.name, "faces", len(me.polygons), "verts", len(me.vertices))

bpy.ops.export_scene.fbx(
    filepath=out_fbx,
    use_selection=False,
    object_types={"MESH", "EMPTY", "ARMATURE", "OTHER"},
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_tspace=False,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="COPY",
    embed_textures=True,
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_NONE",
    bake_space_transform=False,
    use_custom_props=True,
)
print("EXPORTED", out_fbx)
