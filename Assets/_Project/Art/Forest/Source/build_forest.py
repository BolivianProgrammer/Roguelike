"""Forest art only. Run with Blender --background --python this_file.py.
Coordinates in builders are Unity X/Y/Z metres; converted to Blender X/-Z/Y.
Deterministic first-pass assets from ASSETS_NEEDED.md section 5.
"""
import bpy, math, random, json, os
from pathlib import Path
from mathutils import Vector
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
for old in list(bpy.data.scenes):
    if old.name.startswith('Forest_Asset_Review'):
        for ob in list(old.objects): bpy.data.objects.remove(ob,do_unlink=True)
        bpy.data.scenes.remove(old)
for m in list(bpy.data.materials):
    if m.name.startswith(('Forest_','Totem_Glow','Portal_Runes','Shrine_Glow','Shard_Glow')) and m.users==0: bpy.data.materials.remove(m)
for d in ('Models','Textures','Source','Previews'): (ROOT/d).mkdir(parents=True,exist_ok=True)
random.seed(1511)
COLORS=['5E5F63','3E3F44','4E5E3A','5B3E28','33241A','2E3033','8C6A2E','C9A227','7A1F1F','2F4560','C9B99A','26382A','3F5A2C','7A5A2A','6B3FA0','B07CFF',
        '77816A','243024','647342','8B7352','A89471','222128','847F73','444D40','314D35','597344','394A30','70553C','A4A09A','18191B','B5AD96','8B6545']
STONE,SHADOW,MOSS,WOOD,DARK,IRON,BRASS,GOLD,RED,BLUE,LINEN,PINE,OAK,DRY,PURPLE,GLOW=range(16)
def rgb(h): return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
def image_file(name,arr):
    im=bpy.data.images.new(name,width=arr.shape[1],height=arr.shape[0],alpha=True)
    im.pixels.foreach_set(arr.astype(np.float32).ravel()); im.filepath_raw=str(ROOT/'Textures'/name); im.file_format='PNG'; im.save(); return im
palette=np.ones((256,256,4),dtype=np.float32)
for i,c in enumerate(COLORS): palette[(i//8)*64:(i//8+1)*64,(i%8)*32:(i%8+1)*32,:3]=rgb(c)
pal=image_file('Forest_Palette.png',palette)
def material(name,color=None,tex=None,emission=0,alpha=1):
    m=bpy.data.materials.new(name); m.use_nodes=True; bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value=.88
    if color:
        bs.inputs['Base Color'].default_value=(*color,alpha); m.diffuse_color=(*color,alpha)
    if tex:
        n=m.node_tree.nodes.new('ShaderNodeTexImage'); n.image=tex; n.interpolation='Closest'; m.node_tree.links.new(n.outputs['Color'],bs.inputs['Base Color'])
    if emission: bs.inputs['Emission Color'].default_value=(*color,1); bs.inputs['Emission Strength'].default_value=emission
    bs.inputs['Alpha'].default_value=alpha
    return m
MAT=material('Forest_Mat',tex=pal)
CLOTH=material('Forest_Cloth',tex=pal)
WATER=material('Forest_Water',rgb('244743'),alpha=.72)
next(n for n in WATER.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Roughness'].default_value=.18
SPECIAL={n:material(n,rgb(c),emission=.8) for n,c in [('Totem_Glow','B07CFF'),('Portal_Runes','B07CFF'),('Shrine_Glow','D14238'),('Shard_Glow','DADADA')]}

def ground_texture(name,base,tile):
    # Periodic analytic noise, including matching first/last rows and columns.
    q=np.linspace(0,2*math.pi,1024); x,y=np.meshgrid(q,q)
    h=np.zeros_like(x)
    rng=random.Random(42 if name=='Grass' else 72)
    for scale,amp in [(3,.035),(9,.022),(25,.012),(80,.007)]:
        for _ in range(12):
            k=rng.randint(1,scale); l=rng.randint(-scale,scale)
            h+=amp*np.sin(k*x+l*y+rng.random()*6.28)
    a=np.ones((1024,1024,4)); a[:,:,:3]=np.clip(np.array(rgb(base))[None,None,:]*(1+h[:,:,None]),0,1)
    tex=image_file(name+'_BaseColor.png',a)
    # Central periodic derivatives, copied seams; tangent normal in OpenGL convention.
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*2; dy=(np.roll(h,-1,0)-np.roll(h,1,0))*2
    n=np.stack((-dx,-dy,np.ones_like(h)),2); n/=np.linalg.norm(n,axis=2)[:,:,None]
    a[:,:,:3]=n*.5+.5; a[-1]=a[0]; a[:,-1]=a[:,0]
    normal=image_file(name+'_Normal.png',a); normal.colorspace_settings.name='Non-Color'
    m=material('Forest_'+name,tex=tex)
    nd=m.node_tree.nodes.new('ShaderNodeTexImage'); nd.image=normal
    nm=m.node_tree.nodes.new('ShaderNodeNormalMap'); m.node_tree.links.new(nd.outputs['Color'],nm.inputs['Color'])
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); m.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
    return m
GRASS=ground_texture('Grass','4E5E3A',4); DIRT=ground_texture('Dirt','70553C',2)

scene=bpy.data.scenes.new('Forest_Asset_Review'); bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
ASSETS=[]; report=[]; V=[]; F=[]; C=[]; pieces=[]; current=''
def cv(p): return (p[0],-p[2],p[1])
def mesh(v,f,c=STONE):
    off=len(V); V.extend(v); F.extend([tuple(off+i for i in face) for face in f]); C.extend(c if isinstance(c,list) else [c]*len(f))
def box(p,s,c=STONE):
    x,y,z=p; a,b,d=[i/2 for i in s]
    mesh([(x-a,y-b,z-d),(x+a,y-b,z-d),(x+a,y-b,z+d),(x-a,y-b,z+d),(x-a,y+b,z-d),(x+a,y+b,z-d),(x+a,y+b,z+d),(x-a,y+b,z+d)],[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],c)
def beam(a,b,r,c=WOOD,n=6,r2=None):
    a,b=Vector(a),Vector(b); axis=(b-a).normalized(); side=axis.cross(Vector((0,1,0)))
    if side.length<.1: side=axis.cross(Vector((1,0,0)))
    side.normalize(); other=axis.cross(side); vv=[]
    for p,rr in [(a,r),(b,r if r2 is None else r2)]:
        vv.extend([tuple(p+rr*(math.cos(i*2*math.pi/n)*side+math.sin(i*2*math.pi/n)*other)) for i in range(n)])
    ff=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh(vv,ff,[20,c]+[c if i%3 else max(c,0) for i in range(n)])
def lump(p,s,c=STONE,n=7,rings=2):
    x,y,z=p; sx,sy,sz=s; vv=[(x,y-sy/2,z)]
    for j in range(rings):
        t=(j+1)/(rings+1)*math.pi; rr=math.sin(t)
        for i in range(n):
            ang=(i+.15*j)*2*math.pi/n; jitter=random.uniform(.86,1.1)
            vv.append((x+sx/2*rr*math.cos(ang)*jitter,y-sy/2*math.cos(t),z+sz/2*rr*math.sin(ang)*jitter))
    vv.append((x+.06*sx,y+sy/2,z)); top=len(vv)-1
    ff=[(0,1+(i+1)%n,1+i) for i in range(n)]
    for j in range(rings-1):
        a=1+j*n; b=a+n
        ff.extend([(a+i,a+(i+1)%n,b+(i+1)%n,b+i) for i in range(n)])
    ff.extend([(top,1+(rings-1)*n+i,1+(rings-1)*n+(i+1)%n) for i in range(n)])
    colors=[c]*len(ff)
    for i in range(len(colors)):
        if i>=len(colors)-n and c in (STONE,SHADOW): colors[i]=MOSS
        elif random.random()<.18 and c in (PINE,OAK): colors[i]=24 if c==PINE else 25
    mesh(vv,ff,colors)
def ring(p,outer,inner,h,c=STONE,n=12):
    x,y,z=p; vv=[]
    for yy,r in [(y,outer),(y+h,outer),(y,inner),(y+h,inner)]:
        vv.extend([(x+r*math.cos(i*2*math.pi/n),yy,z+r*math.sin(i*2*math.pi/n)) for i in range(n)])
    ff=[]
    for i in range(n):
        j=(i+1)%n
        ff.extend([(i,j,n+j,n+i),(n+i,n+j,3*n+j,3*n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j)])
    mesh(vv,ff,c)
def flush(name=None,mat=None,pivot=(0,0,0),tile=None):
    global V,F,C
    if not V:return None
    me=bpy.data.meshes.new((name or current)+'_Mesh'); me.from_pydata([cv(tuple(v[i]-pivot[i] for i in range(3))) for v in V],[],F); me.update()
    ob=bpy.data.objects.new(name or current,me); scene.collection.objects.link(ob); ob.location=cv(pivot)
    me.materials.append(mat or MAT); uv=me.uv_layers.new(name='UVMap')
    for poly,c in zip(me.polygons,C):
        for li in poly.loop_indices:
            if tile:
                co=V[me.loops[li].vertex_index]; uv.data[li].uv=(co[0]/tile,co[2]/tile)
            else: uv.data[li].uv=((c%8+.5)/8,(c//8+.5)/4)
    pieces.append(ob); V=[];F=[];C=[]; return ob
def begin(name,budget,collision='Box collider is enough.'):
    global current,pieces,bud,note
    current=name;pieces=[];bud=budget;note=collision
def collider(build,name=None):
    flush(); build(); o=flush(name or current+'_Collision'); o.display_type='WIRE'; o.hide_render=True; return o
def finish():
    flush(); visible=[o for o in pieces if not o.name.endswith('_Collision')]
    bpy.context.view_layer.update()
    root=next((o for o in visible if o.name==current),visible[0]); root.name=current
    for o in pieces:
        if o!=root:
            mx=o.matrix_world.copy(); o.parent=root; o.matrix_world=mx
    bpy.context.view_layer.update()
    tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in visible)
    coltris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in pieces if o not in visible)
    coords=[o.matrix_world@Vector(c) for o in visible for c in o.bound_box]
    size=[max(v[i] for v in coords)-min(v[i] for v in coords) for i in (0,2,1)]
    assert tris<=bud,(current,tris,bud)
    assert coltris<100,(current,'collision budget',coltris)
    bpy.ops.object.select_all(action='DESELECT')
    for o in pieces: o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Models'/(current+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
    report.append(dict(name=current,triangles=tris,budget=bud,collision_triangles=coltris,size_unity=[round(v,3) for v in size],collision=note,children=[o.name for o in pieces if o!=root]))
    # Free the shared per-spec collision name before the next tree variant.
    for o in pieces:
        if o.name in ('Tree_Pine_Collision','Tree_Oak_Collision'):o.name=current+'_Collision'
    ASSETS.append((root,list(pieces)))
    for o in pieces: o.hide_set(True)
    print(current,tris,'tris')

def trees():
    for j in range(3):
        begin('Tree_Pine_'+chr(65+j),600,'Tree_Pine_Collision: trunk only, r 0.3 m, height 3 m.')
        h=[9,8.5,9][j]; lean=.85 if j==2 else 0
        beam((0,0,0),(lean,h*.86,0),.33,WOOD,8,.1)
        for k in range(4):
            y=2+k*1.5; rr=(2-k*.37)*(.84 if j==0 else 1)
            if j==2 and k<2:
                for side in (-1,1): beam((lean*y/h,y,0),(side*1.45,y+.5,.3),.09,DARK,5,.025)
            else:
                n=9; vv=[(lean*y/h+rr*math.cos(i*2*math.pi/n),y+(.12 if i%2 else 0),rr*math.sin(i*2*math.pi/n)) for i in range(n)]+[(lean*(y+2.5)/h,y+2.5,0)]
                mesh(vv,[(i,(i+1)%n,n) for i in range(n)]+[tuple(reversed(range(n)))],[PINE if i%3 else 24 for i in range(n)]+[PINE])
        collider(lambda:beam((0,0,0),(0,3,0),.3,WOOD,8), 'Tree_Pine_Collision');finish()
    for j in range(2):
        begin('Tree_Oak_'+chr(65+j),800,'Tree_Oak_Collision: trunk only, r 0.4 m, height 3 m.')
        beam((0,0,0),(.35,3.4,.1),.48,WOOD,7,.29);beam((.35,3.4,.1),(-.3,6,0),.29,WOOD,7,.12)
        for k in range(5):
            a=k*2*math.pi/5+j*.5; x,z=1.65*math.cos(a),1.65*math.sin(a)
            beam((.2,3.1,0),(x,5.7,z),.2,WOOD,6,.08)
            beam((0,.2,0),(math.cos(a)*1.0,.04,math.sin(a)*1.0),.23,WOOD,5,.04)
            lump((x,5.8+(k%2)*.5,z),(3.2,3.1,3.2),OAK,7)
        lump((0,7,0),(3.8,2,3.7),OAK,7)
        collider(lambda:beam((0,0,0),(0,3,0),.4,WOOD,8),'Tree_Oak_Collision'); finish()
    for j in range(2):
        begin('Bush_'+chr(65+j),150,'None (foliage).')
        for x,z,s in [(-.35,0,.8),(.3,.2,1),(.1,-.3,.85)]: lump((x,.4,z),(s,.8,s),OAK if j==0 else PINE,6)
        finish()
    begin('Fern',150,'None (foliage).')
    for k in range(8):
        a=k*math.pi/4; d=Vector((math.cos(a),0,math.sin(a))); tangent=Vector((-math.sin(a),0,math.cos(a)))
        for j in range(3):
            p=d*(.09+j*.15)+Vector((0,.22+j*.12,0)); end=d*(.3+j*.14)+Vector((0,.24+j*.08,0)); w=.15*(1-j*.25)
            mesh([tuple(p),tuple(p+tangent*w+d*.07),tuple(end+Vector((0,.035,0))),tuple(p-tangent*w+d*.07)],[(0,1,2),(0,2,3)],OAK if k%2 else 25)
    finish()

def terrain():
    for j in range(3):
        begin('Ground_Mound_'+chr(65+j),150,'Simplified walkable mound; slopes below 25 degrees, base -0.3 m.')
        n=12; rx=[4,4.8,6][j]; rz=rx*.9; high=1.1+j*.15
        vv=[(0,high,0)]
        for rr,yy in [(.35,high*.6),(1,-.3)]:
            vv.extend([(rx*rr*math.cos(i*2*math.pi/n),yy,rz*rr*math.sin(i*2*math.pi/n)) for i in range(n)])
        ff=[(0,1+i,1+(i+1)%n) for i in range(n)]
        ff += [(1+i,13+i,13+(i+1)%n,1+(i+1)%n) for i in range(n)]
        mesh(vv,ff,MOSS);flush(mat=GRASS,tile=4); collider(lambda:mesh(vv,ff));finish()
    for j in range(3):
        begin('Rock_'+chr(65+j),120,'Box collider is enough.' if j==0 else 'Simplified rock collision mesh.')
        if j<2: lump((0,[.4,.6][j],0),(1.65,[.8,1.2][j],1.5),STONE,7)
        else:
            for x,y,z,s in [(-.35,.35,0,.9),(.35,.25,.25,.7),(.2,.4,-.3,.85)]:lump((x,y,z),(s,y*2,s),STONE,6)
        if j: collider(lambda:lump((0,.5,0),(1.5,1,1.5),STONE,6))
        finish()
    for bend in (False,True):
        def centre(t):
            a=t*math.pi/6; r=6/(math.pi/6)
            return (r*(1-math.cos(a)),r*math.sin(a)-3) if bend else (0,t*6-3)
        begin('Path_Dirt_'+('Bend' if bend else 'Straight'),80,'None. Edge vertex alpha in color layer; use vertex-alpha material to feather.')
        vv=[]
        for i in range(7):
            t=i/6; x,z=centre(t); a=t*math.pi/6 if bend else 0
            for w in (-1.75,-1.48,1.48,1.75): vv.append((x+w*math.cos(a),.02,z-w*math.sin(a)))
        ff=[(i*4+j,(i+1)*4+j,(i+1)*4+j+1,i*4+j+1) for i in range(6) for j in range(3)]
        mesh(vv,ff);o=flush(mat=DIRT,tile=2)
        ca=o.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
        for i,d in enumerate(ca.data):d.color=(1,1,1,0 if i%4 in (0,3) else 1)
        finish()
        begin('Stream_'+('Bend' if bend else 'Straight'),250,'Bed and banks collision; separate Forest_Water surface has no collision.')
        vv=[]
        for i in range(7):
            t=i/6;x,z=centre(t);a=t*math.pi/6 if bend else 0
            for w,y in [(-1.5,0),(-1,-.4),(1,-.4),(1.5,0)]:vv.append((x+w*math.cos(a),y,z-w*math.sin(a)))
        mesh(vv,ff,WOOD);flush();collider(lambda:mesh(vv,ff))
        for i in range(6):
            x,z=centre(i/6); xx,zz=centre((i+1)/6)
            mesh([(x-1.17,-.22,z),(x+1.17,-.22,z),(xx+1.17,-.22,zz),(xx-1.17,-.22,zz)],[(0,1,2,3)],SHADOW)
        flush(current+'_Water',WATER)
        for i in range(5):
            x,z=centre((i+.5)/6);lump((x+(-1 if i%2 else 1)*1.3,-.02,z),(.35,.25,.3),STONE,5,1)
        flush(current+'_BankStones');finish()

def camp():
    begin('Log_Fallen',150,'Log_Fallen_Collision: low-poly capsule-like trunk.')
    beam((-1.5,.35,0),(1.5,.35,0),.35,WOOD,9,.26)
    beam((-.4,.45,0),(-.6,.75,.25),.12,DARK,5,.03)
    box((0,.63,0),(1.8,.04,.22),MOSS)
    for x in (.3,.6):lump((x,.38,-.26),(.35,.08,.28),LINEN,5,1)
    collider(lambda:beam((-1.5,.35,0),(1.5,.35,0),.34,WOOD,8));finish()
    begin('Palisade_Section',200)
    for i in range(7):
        x=-1.3+i*.43; top=2.6-(i%2)*.12
        box((x,(top-.35)/2,0),(.38,top-.35,.38),WOOD)
        mesh([(x-.19,top-.35,-.19),(x+.19,top-.35,-.19),(x+.19,top-.35,.19),(x-.19,top-.35,.19),(x,top,0)],[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],20)
    for y in (.65,1.7):box((0,y,.16),(3,.14,.12),DARK)
    for x in (-1.1,0,1.1):box((x,1.7,.23),(.09,.28,.05),LINEN)
    finish()
    begin('Palisade_Gate',200,'Two post collision boxes and lintel; 3 m clear opening.')
    for x in (-1.65,1.65):beam((x,0,0),(x,2.6,0),.2,WOOD,5,.08)
    box((0,2.42,0),(3.7,.3,.32),DARK)
    collider(lambda:[box((x,1.15,0),(.3,2.3,.35)) for x in (-1.65,1.65)]);finish()
    begin('Tent',300,'Tent_Collision: box around tent body.')
    for z in (-1.4,1.4):beam((0,0,z),(0,2.2,z),.06,WOOD,6)
    beam((0,2.2,-1.6),(0,2.2,1.6),.055,WOOD,6)
    for x in (-1.5,1.5):
        for z in (-1.5,1.5):beam((x*.75,.5,z*.8),(x,0,z),.012,LINEN,4);beam((x,-.04,z),(x,.15,z),.04,WOOD,4)
    flush()
    mesh([(-1.4,.04,-1.4),(1.4,.04,-1.4),(0,2.16,-1.4),(-1.4,.04,1.4),(1.4,.04,1.4),(0,2.16,1.4),(-.45,.04,1.42),(.45,.04,1.42)],[(0,3,5,2),(2,5,4,1),(0,2,1),(3,6,5),(5,7,4)],LINEN)
    flush('Tent_Canvas',CLOTH);collider(lambda:box((0,1,0),(2.8,2,2.8)));finish()
    begin('Log_Pile',250)
    for y,zs in [(.23,[-.46,0,.46]),(.64,[-.23,.23]),(1.05,[0])]:
        for z in zs:beam((-1,y,z),(1,y,z),.23,WOOD,7)
    finish()
    begin('Chopping_Block',150)
    beam((0,0,0),(0,.68,0),.35,WOOD,9,.3)
    beam((.03,.55,0),(.22,1.2,0),.035,WOOD,5)
    mesh([(-.28,.73,-.035),(.14,.81,-.035),(.14,1.02,-.035),(-.28,1.09,-.035),(-.28,.73,.035),(.14,.81,.035),(.14,1.02,.035),(-.28,1.09,.035)],[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],IRON);finish()
    begin('Campfire',200,'None. Fire VFX centre at origin.')
    beam((-.4,.12,-.2),(.4,.12,.2),.12,DARK,6);beam((-.4,.18,.2),(.4,.18,-.2),.12,DARK,6)
    for i in range(8):a=i*math.pi/4;lump((.54*math.cos(a),.15,.54*math.sin(a)),(.3,.3,.3),SHADOW,5,1)
    finish()
    begin('Bridge_Wooden',600,'Bridge_Wooden_Collision: deck, end ramps and rails.')
    for i in range(21):box((0,.29,-3+i*.3),(3,.12,.28),WOOD if i%3 else 27)
    for side in (-1,1):
        for z in (-3,-1,1,3):box((side*1.48,.7,z),(.14,1.0,.14),DARK)
        box((side*1.48,1.13,0),(.12,.14,6.2),WOOD)
        box((side*1.1,.13,0),(.18,.2,6.2),DARK)
    def ramps():
        for sign in (-1,1):mesh([(-1.5,0,sign*4.5),(1.5,0,sign*4.5),(1.5,.35,sign*3.1),(-1.5,.35,sign*3.1)],[(0,1,2,3)],WOOD)
    ramps()
    def col():
        box((0,.2,0),(3.2,.3,6.2));ramps()
        for x in (-1.48,1.48):box((x,.75,0),(.14,.9,6.2))
    collider(col);finish()

def wall_detail(w,h,z):
    for i in range(max(2,int(w/2)-1)):
        x=-w/2+1+i*2;box((x,.28,z),(.9,.23,.04),MOSS)
        box((x+.25,h*.45+(i%3)*.35,z),(.5,.2,.04),22)
    for x in (-w*.25,w*.25):
        box((x,h*.62,z+.02),(.12,1.1,.04),29);box((x,h*.62,z+.045),(.4,.1,.02),29)
def battlements(w,y,z,depth=.65):
    n=max(2,int(w/1.6))
    for i in range(n):box((-w/2+.5+i*(w-1)/(n-1),y,z),(.8,.8,depth),STONE)
def arch(x,y,z,r,thick,depth,n=8):
    for i in range(n):
        a=i*math.pi/n;b=(i+1)*math.pi/n
        q=[(x+rr*math.cos(t),y+rr*math.sin(t),zz) for zz in (z-depth/2,z+depth/2) for rr,t in [(r,a),(r,b),(r+thick,b),(r+thick,a)]]
        mesh(q,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],STONE if i%2 else 22)
def castle():
    for short in (False,True):
        w=12 if short else 28;begin('Castle_Wall'+('_Short' if short else ''),300 if short else 600)
        box((0,3.35,0),(w,6.7,1.6));box((0,6.6,-.4),(w,.3,.8),SHADOW);battlements(w,7.1,.45);wall_detail(w,6.7,.82);finish()
    begin('Castle_Tower',900,'Castle_Tower_Collision: 12-sided cylinder. Decorative doorway; tower interior not playable.')
    beam((0,0,0),(0,10.1,0),2.65,STONE,12)
    ring((0,9.3,0),2.8,2.45,.45,22,12)
    for i in range(12):
        a=i*math.pi/6;x,z=2.45*math.cos(a),2.45*math.sin(a)
        box((x,10.55,z),(.65,.9,.65),STONE)
        box((x,9.05,z),(.35,.5,.35),SHADOW)
    box((0,1.35,2.57),(1.4,2.7,.04),DARK);arch(0,2.1,2.6,.7,.2,.12,6)
    for y in (4.6,7.5):box((0,y,2.66),(.18,1.15,.03),29)
    collider(lambda:beam((0,0,0),(0,10.8,0),2.65,STONE,12));finish()
    begin('Castle_Gatehouse',900,'Two towers + lintel collision; clear opening 4 m wide, 5 m high.')
    for x in (-3.25,3.25):
        box((x,3.7,0),(2.5,7.4,3.4));
        for dx in (-.8,.8):box((x+dx,7.7,1.35),(.65,.6,.7))
        box((x,4.7,1.72),(.15,1.1,.04),29)
    box((0,7.2,0),(4,1.2,3.4));arch(0,5,0,2,.45,3.4,8)
    # Arch minimum overhead at centre is 5.2 m. Portcullis raised to >= 5.2 m.
    for x in (-1.65,-1.1,-.55,0,.55,1.1,1.65):box((x,6.05,0),(.07,1.65,.07),IRON)
    for y in (5.55,6.2,6.7):box((0,y,0),(3.5,.07,.07),IRON)
    def col():
        for x in (-3.25,3.25):box((x,3.7,0),(2.5,7.4,3.4))
        box((0,6.4,0),(4,2.4,3.4))
    collider(col);finish()
    begin('Drawbridge',150,'Box collider is enough; walkable. Pivot at hinge, -Z end of deck.')
    for i in range(8):box((0,.15,.375+i*.75),(4,.3,.72),WOOD)
    for x in (-1.6,1.6):
        box((x,.31,3),(.12,.03,6),IRON)
        beam((x,.3,5.7),(x,2.7,.1),.025,IRON,4)
    finish()
    begin('Castle_Keep',1200,'Walls + roof collision; 2 m × 2.8 m open entrance on -Z. Empty interior.')
    for x in (-4.6,4.6):box((x,6,0),(.8,12,8))
    box((0,6,3.6),(8.4,12,.8))
    for x in (-2.8,2.8):box((x,6,-3.6),(3.6,12,.8))
    box((0,7.4,-3.6),(2,9.2,.8));box((0,11.9,0),(9.2,.2,7.2),SHADOW)
    for z in (-3.65,3.65):battlements(10,12.5,z,.7)
    for x in (-4.65,4.65):
        for z in (-2,0,2):box((x,12.5,z),(.7,1,.9))
    for x in (-4.4,4.4):
        for z in (-3.55,3.55):box((x,3,z),(.9,6,.9),SHADOW)
    for x in (-2.5,2.5):
        for y in (5,8.5):box((x,y,-4.02),(.45,1.4,.04),29)
    def col():
        for x in (-4.6,4.6):box((x,6,0),(.8,12,8))
        box((0,6,3.6),(8.4,12,.8))
        for x in (-2.8,2.8):box((x,6,-3.6),(3.6,12,.8))
        box((0,7.4,-3.6),(2,9.2,.8));box((0,11.9,0),(9.2,.2,7.2))
    collider(col);finish()
    begin('Castle_Well',250,'Castle_Well_Collision: stone ring only, height 0.9 m.')
    ring((0,0,0),.8,.55,.9,STONE,8)
    for x in (-.65,.65):box((x,1.04,0),(.1,.9,.1),WOOD)
    mesh([(-.8,1.3,-.65),(.8,1.3,-.65),(0,1.5,-.65),(-.8,1.3,.65),(.8,1.3,.65),(0,1.5,.65)],[(0,3,5,2),(2,5,4,1),(0,2,1),(3,4,5)],WOOD)
    beam((.2,.7,0),(.2,.96,0),.12,WOOD,6);collider(lambda:ring((0,0,0),.8,.55,.9,STONE,8));finish()
    begin('Weapon_Stand',250)
    for x in (-.85,.85):box((x,.8,0),(.13,1.6,.15),WOOD);box((x,.07,0),(.3,.14,.4),DARK)
    for y in (.5,1.3):box((0,y,0),(2,.13,.13),WOOD)
    for x in (-.6,0,.6):beam((x,.1,.08),(x,1.6,.08),.025,WOOD,5);beam((x,1.6,.08),(x,1.8,.08),.075,IRON,4,0)
    beam((.35,.85,.1),(.35,.85,.17),.3,IRON,8);finish()
    begin('Banner',40,'None. Pivot top centre. Cloth extends downward 3 m.')
    mesh([(-.7,0,0),(.7,0,0),(-.7,-2.6,.015),(.7,-2.6,.015),(0,-3,.025)],[(0,1,3,2),(2,3,4)],RED)
    mesh([(0,-.65,.035),(.23,-1.2,.035),(0,-1.9,.035),(-.23,-1.2,.035)],[(0,1,2,3)],29)
    flush(mat=CLOTH);finish()

def chapel():
    begin('Chapel_Ruin',1500,'Simplified walls, altar and rubble collision; west (-X) side open. Two pointed windows in east wall.')
    # Nave runs along Z. East wall has two genuine pointed openings.
    for z,length,h in [(-6,3,3.6),(0,3,4),(6,3,2.7)]:box((4.65,h/2,z),(.7,h,length))
    for z in (-3,3):
        box((4.65,.5,z),(.7,1,3))
        # pointed window arch, built in Y/Z plane
        for side in (-1,1):
            a=(4.65,2.4,z+side*1.5); b=(4.65,3.65,z)
            beam(a,b,.19,STONE,4)
    for x,w,h in [(-3.5,3,1.6),(0,4,3.1),(3.8,2.4,3.8)]:box((x,h/2,-7.1),(w,h,.8))
    for x,w,h in [(-3.8,2.4,.8),(0,3,2.1),(3.8,2.4,3.3)]:box((x,h/2,7.1),(w,h,.8))
    box((2.5,.65,4.5),(1.8,1.3,1.1),SHADOW);box((2.5,1.35,4.5),(2,.15,1.3),STONE)
    for i in range(7):lump((-4+random.random()*3,.25,-5+i*1.4),(1.1,.5,.8),STONE,5,1)
    # Fallen arch stones, kept low on the collapsed west edge.
    for i in range(6):
        a=i*math.pi/5;box((-3+1.1*math.cos(a),.3,1+1.1*math.sin(a)),(.55,.6,.55),22)
    for i in range(7):lump((4.24,.5+i*.45,-5+.15*math.sin(i)),(.07,.6,.6),MOSS,5,1)
    def col():
        box((4.65,1.5,0),(.7,3,15));box((0,1,-7.1),(10,2,.8));box((0,.6,7.1),(10,1.2,.8));box((2.5,.7,4.5),(2,1.4,1.3))
        for z in (-4,1,5):box((-3.5,.25,z),(1.2,.5,1.2))
    collider(col);finish()
    for j in range(3):
        begin('Standing_Stone_'+chr(65+j),80)
        beam((0,0,0),(.2 if j==1 else 0,2.8 if j!=2 else 2.1,0),.5,STONE,5,.34)
        # angular spiral carving, shallow contrasting strips on the front
        for a,b in [((-.18,1.2,.41),(.18,1.2,.41)),((.18,1.2,.41),(.18,1.7,.37)),((.18,1.7,.37),(-.12,1.7,.37)),((-.12,1.7,.37),(-.12,1.4,.4))]:beam(a,b,.014,MOSS,3)
        finish()

def objectives():
    begin('Totem_Base',150);beam((0,0,0),(0,.4,0),.8,SHADOW,8,.72);finish()
    begin('Totem',400,'Box collider is enough. Pivot own bottom; place at y=0.4 on base. Totem_Glow crystals separate.')
    beam((0,0,0),(0,1.95,0),.26,DARK,7,.18)
    for y in (.4,1.35):ring((0,y,0),.3,.26,.09,IRON,7)
    for x,y in [(-.23,.9),(.23,1.5)]:
        lump((x,y,.17),(.42,.45,.36),LINEN,6,1)
        for dx in (-.09,.09):box((x+dx,y+.03,.335),(.075,.07,.035),29)
    beam((-.45,1.65,0),(.45,1.65,0),.06,LINEN,5)
    flush()
    for x,y,z in [(0,1.83,0),(-.32,.8,-.1),(.32,1.2,-.1)]:lump((x,y,z),(.24,.54,.24),GLOW,5,1)
    flush('Totem_Crystals',SPECIAL['Totem_Glow']);finish()
    begin('Totem_Broken',300,'Stump root with separate Chunk_1..Chunk_4 mesh children; add chunk physics later.')
    beam((0,0,0),(0,.6,0),.26,DARK,7,.15);flush()
    for i,(x,z) in enumerate([(-.5,0),(.4,.3),(.1,-.5),(-.3,.5)]):
        beam((x,.1,z),(x+.2,.3,z+.1),.17,WOOD,6,.09);flush('Chunk_'+str(i+1),pivot=(x,.2,z))
    finish()
    begin('Extraction_Portal',400,'Two pillar collision meshes combined; empty portal opening. Portal_Runes separate.')
    for x in (-1.95,1.95):beam((x,0,0),(x,2.7,0),.5,STONE,5,.4)
    box((0,2.75,0),(5,.5,1),SHADOW);ring((0,0,0),1.5,1.35,.06,STONE,12);flush()
    for x in (-1.95,0,1.95):
        for a,b in [((x-.14,2.65,.51),(x,2.87,.51)),((x,2.87,.51),(x+.14,2.65,.51))]:beam(a,b,.025,GLOW,3)
    flush('Extraction_Portal_Runes',SPECIAL['Portal_Runes'])
    collider(lambda:[box((x,1.35,0),(.85,2.7,.85)) for x in (-1.95,1.95)]);finish()
    begin('Treasure_Chest',300,'Box collider is enough. Lid child hinge at (0,0.4,-0.275), rotates Unity local X -110 degrees.')
    # Hollow body: the opening lid reveals an actual cavity.
    box((0,.04,0),(.9,.08,.55),DARK)
    for x in (-.41,.41):box((x,.24,0),(.08,.4,.55),WOOD)
    for z in (-.235,.235):box((0,.24,z),(.74,.4,.08),WOOD)
    for x in (-.31,.31):
        for z in (-.279,.279):box((x,.22,z),(.06,.42,.015),IRON)
    box((0,.32,.295),(.13,.15,.035),BRASS);flush()
    # Low arched lid, independent origin on rear hinge.
    vv=[]
    for x in (-.45,.45):vv.extend([(x,.4,-.275),(x,.55,-.19),(x,.6,0),(x,.55,.19),(x,.4,.275)])
    mesh(vv,[(0,1,2,3,4),(9,8,7,6,5)]+[(i,i+5,i+6,i+1) for i in range(4)]+[(0,4,9,5)],WOOD)
    for x in (-.31,.31):
        for z,y in [(-.19,.555),(0,.605),(.19,.555)]:box((x,y,z),(.06,.018,.15),IRON)
    flush('Lid',pivot=(0,.4,-.275));finish()
    begin('Challenge_Shrine',300,'Box collider is enough. Separate Glyph child uses Shrine_Glow, front +Z.')
    box((0,.12,0),(.8,.24,.8),SHADOW);box((0,.34,0),(.65,.2,.65),STONE)
    beam((0,.44,0),(0,2.05,0),.27,SHADOW,5,.2);lump((0,2.05,0),(.52,.3,.52),STONE,5,1);flush()
    for a,b in [((0,1.85,.25),(.15,1.45,.29)),((.15,1.45,.29),(0,1.05,.31)),((0,1.05,.31),(-.15,1.45,.29)),((-.15,1.45,.29),(0,1.85,.25))]:beam(a,b,.025,RED,4)
    flush('Glyph',SPECIAL['Shrine_Glow']);finish()
    begin('Loot_Shard',60,'None. Pivot at centre; white material for runtime tint.')
    lump((0,0,0),(.15,.25,.15),30,5,1);flush(mat=SPECIAL['Shard_Glow']);finish()

def build():
    trees();terrain();camp();chapel();castle();objectives()
    (ROOT/'Source'/'forest_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    lines=['# Mission Forest — first art pass','','46 assets from ASSETS_NEEDED.md section 5. Asset creation only; no scenes, prefabs, gameplay or Unity import settings changed.','',
      '## Delivery','One FBX per asset. Units metres; FBX -Z forward / Y up. Coordinates below are Unity X × Y × Z. Props use ground-centre pivots except Banner (top), Drawbridge (hinge), Loot_Shard (centre), and named articulated children.','',
      'Shared Forest_Mat uses Textures/Forest_Palette.png: Point filtering recommended. Forest_Cloth uses the same palette. Grass and Dirt base colour/normal pairs are 1024² seamless textures, OpenGL tangent normals, tiles 4 m and 2 m respectively. Normal textures must be imported as Normal Map.','',
      'Forest_Water is a separate surface/material; configure URP transparency when integrating. Totem_Glow, Portal_Runes, Shrine_Glow and Shard_Glow are distinct emission materials. FBX cannot configure URP emission/transparency automatically. Path edge alpha is stored in vertex Color; the engine material must read it for soft edges.','',
      'Collision meshes are named *_Collision, excluded from render previews. Disable their renderers and attach appropriate MeshColliders during integration. Multiple disconnected convex pieces can be split for physics if required. Budgets below count visible triangles separately.','',
      '| File | Visible tris / budget | Collision tris | Size X × Y × Z (m) | Collision / contract |','|---|---:|---:|---|---|']
    for r in report:lines.append('| '+r['name']+'.fbx | '+str(r['triangles'])+' / '+str(r['budget'])+' | '+str(r['collision_triangles'])+' | '+' × '.join(map(str,r['size_unity']))+' | '+r['collision']+' |')
    lines += ['', '## Sources and review','Source/build_forest.py rebuilds this set in Blender (no external generator or paid service). Source/Forest_Assets.blend is the editable asset gallery. Each asset is spaced out for review in that file; the exported FBXs are individually at their documented origin.', '', 'Source/forest_report.json records actual triangle counts, bounds, and named children. This is a practical first pass: facade cracks, cloth wear, chain links and some small ornamental details are simplified. No Unity runtime validation or scene integration was performed.']
    (ROOT/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    # Review gallery, not a level. Originals remain untouched in their own scene.
    for i,(root,obs) in enumerate(ASSETS):
        root.location += Vector(((i%8)*16,(i//8)*20,0))
        for o in obs:
            o.hide_set(o.name.endswith('_Collision'))
    for area in bpy.context.screen.areas if bpy.context.screen else []:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.region_3d.view_distance=110
            area.spaces.active.region_3d.view_location=(50,50,0)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source'/'Forest_Assets.blend'))
    print('COMPLETE',len(report),'assets',sum(r['triangles'] for r in report),'visible triangles')

if __name__=='__main__':build()
