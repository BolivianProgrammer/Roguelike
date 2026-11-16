"""Render a normalized contact sheet of the source gallery (not a game level)."""
import bpy, math
from mathutils import Vector, Matrix
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
source=bpy.data.scenes['Forest_Asset_Review']
preview=bpy.data.scenes.new('Forest_Contact_Sheet');bpy.context.window.scene=preview
preview.render.engine='CYCLES';preview.cycles.samples=20
preview.cycles.use_denoising=True
preview.render.resolution_x=2200;preview.render.resolution_y=1700;preview.render.resolution_percentage=100
preview.world=bpy.data.worlds.new('Forest_Preview_World');preview.world.use_nodes=True
preview.world.node_tree.nodes.get('Background').inputs[0].default_value=(.12,.14,.18,1)
preview.world.node_tree.nodes.get('Background').inputs[1].default_value=.7
roots=[o for o in source.objects if not o.parent and '_Collision' not in o.name]
view=Vector((.7,-1, .8)).normalized();right=Vector((1,.7,0)).normalized();up=view.cross(right).normalized()
for idx,root in enumerate(roots):
    obs=[o for o in [root,*root.children_recursive] if '_Collision' not in o.name]
    bounds=[o.matrix_world@Vector(v) for o in obs for v in o.bound_box]
    low=Vector(tuple(min(v[a] for v in bounds) for a in range(3)));high=Vector(tuple(max(v[a] for v in bounds) for a in range(3)))
    center=(low+high)/2;s=3.4/max(high-low)
    target=right*((idx%8)*5)+up*(-(idx//8)*5)
    for o in obs:
        dup=bpy.data.objects.new(o.name+'_Preview',o.data);preview.collection.objects.link(dup)
        dup.matrix_world=Matrix.Translation(target)@Matrix.Scale(s,4)@Matrix.Translation(-center)@o.matrix_world
    font=bpy.data.curves.new(root.name+'_Label','FONT');font.body=root.name;font.align_x='CENTER';font.size=.22
    label=bpy.data.objects.new(root.name+'_Label',font);preview.collection.objects.link(label)
    label.location=target-up*2.05;label.rotation_euler=view.to_track_quat('Z','Y').to_euler()
    m=bpy.data.materials.get('Preview_Label')
    if not m:
        m=bpy.data.materials.new('Preview_Label');m.diffuse_color=(.8,.85,.9,1)
    font.materials.append(m)
center=right*17.5-up*12.5
cam=bpy.data.cameras.new('Contact_Camera');ob=bpy.data.objects.new('Contact_Camera',cam);preview.collection.objects.link(ob)
ob.location=center+view*70;ob.rotation_euler=(-view).to_track_quat('-Z','Y').to_euler();cam.type='ORTHO';cam.ortho_scale=41;preview.camera=ob
for name,offset,power,size in [('Key',view*20-up*4-right*10,24000,22),('Fill',view*12+right*16,15000,20)]:
    ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.shape='DISK';ld.size=size
    lo=bpy.data.objects.new(name,ld);preview.collection.objects.link(lo);lo.location=center+offset;lo.rotation_euler=(center-lo.location).to_track_quat('-Z','Y').to_euler()
preview.render.image_settings.file_format='PNG';preview.render.filepath=str(ROOT/'Previews'/'Forest_Contact_Sheet.png')
bpy.ops.render.render(write_still=True)
bpy.context.window.scene=source
for o in source.objects:o.hide_set('_Collision' in o.name)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source'/'Forest_Assets.blend'))
print('Saved Forest_Contact_Sheet.png and source gallery.')
