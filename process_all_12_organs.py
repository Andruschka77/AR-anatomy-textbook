import struct, zlib, os, trimesh
import numpy as np

def parse_all_geometries(filename):
    with open(filename, 'rb') as f:
        data = f.read()

    def read_node(offset):
        if offset + 13 > len(data): return None, offset
        end_offset, num_props, prop_list_len = struct.unpack('<III', data[offset:offset+12])
        name_len = data[offset+12]
        if end_offset == 0: return None, offset + 13
        name = data[offset+13:offset+13+name_len].decode('ascii', errors='ignore')
        prop_offset = offset + 13 + name_len
        child_offset = prop_offset + prop_list_len
        return (name, prop_offset, prop_list_len, child_offset, end_offset), end_offset

    def read_props(offset, end_prop_offset):
        props = []
        p = offset
        while p < end_prop_offset:
            type_code = chr(data[p])
            p += 1
            if type_code in ('S', 'R'):
                slen = struct.unpack('<I', data[p:p+4])[0]
                val = data[p+4:p+4+slen]
                props.append(val.decode('utf-8', errors='ignore'))
                p += 4 + slen
            elif type_code == 'L':
                props.append(struct.unpack('<q', data[p:p+8])[0])
                p += 8
            elif type_code == 'I':
                props.append(struct.unpack('<i', data[p:p+4])[0])
                p += 4
            elif type_code in ('d', 'f', 'i', 'l', 'b'):
                arr_len, enc, comp_len = struct.unpack('<III', data[p:p+12])
                p += 12 + comp_len
                props.append(f'array_{type_code}_{arr_len}')
            else:
                break
        return props

    def read_array_prop(p):
        type_code = chr(data[p])
        p += 1
        arr_len, enc, comp_len = struct.unpack('<III', data[p:p+12])
        p += 12
        raw = data[p:p+comp_len]
        if enc == 1:
            raw = zlib.decompress(raw)
        return type_code, arr_len, raw

    curr = 27
    geoms = {}
    while curr < len(data) - 13:
        node, next_off = read_node(curr)
        if node is None: break
        if node[0] == 'Objects':
            obj_curr = node[3]
            while obj_curr < node[4] - 13:
                sub, next_sub = read_node(obj_curr)
                if sub is None: break
                if sub[0] == 'Geometry':
                    props = read_props(sub[1], sub[3])
                    geom_id = props[0]
                    geom_name = props[1].split('\x00')[0] if len(props) > 1 and isinstance(props[1], str) else str(geom_id)
                    
                    verts = None
                    indices = None
                    gcurr = sub[3]
                    while gcurr < sub[4] - 13:
                        gnode, gnext = read_node(gcurr)
                        if gnode is None: break
                        if gnode[0] == 'Vertices':
                            _, vlen, vraw = read_array_prop(gnode[1])
                            verts = struct.unpack(f'<{vlen}d', vraw)
                        elif gnode[0] == 'PolygonVertexIndex':
                            _, ilen, iraw = read_array_prop(gnode[1])
                            indices = struct.unpack(f'<{ilen}i', iraw)
                        gcurr = gnext
                    
                    if verts and indices:
                        geoms[geom_name] = (verts, indices)
                obj_curr = next_sub
            break
        curr = next_off
    return geoms

def build_trimesh(verts, indices):
    out_verts = np.array(verts, dtype=np.float64).reshape((-1, 3))
    out_faces = []
    current_poly = []
    for idx in indices:
        if idx < 0:
            current_poly.append(~idx)
            for k in range(1, len(current_poly) - 1):
                out_faces.append([current_poly[0], current_poly[k], current_poly[k+1]])
            current_poly = []
        else:
            current_poly.append(idx)
    return trimesh.Trimesh(vertices=out_verts, faces=out_faces, process=True)

def slice_mesh_by_mask(mesh, mask, filename, center_offset=None):
    # mask: boolean mask of vertices to keep
    # faces where all or majority of vertices are in mask
    face_mask = np.mean(mask[mesh.faces], axis=1) >= 0.5
    sub_faces = mesh.faces[face_mask]
    if len(sub_faces) == 0:
        print(f"Warning: no faces for {filename}")
        return
    # reindex
    used_indices = np.unique(sub_faces)
    index_map = np.full(len(mesh.vertices), -1, dtype=np.int32)
    index_map[used_indices] = np.arange(len(used_indices))
    
    new_verts = mesh.vertices[used_indices].copy()
    if center_offset is not None:
        new_verts -= center_offset
    new_faces = index_map[sub_faces]
    
    sub_mesh = trimesh.Trimesh(vertices=new_verts, faces=new_faces, process=True)
    sub_mesh.export(filename)
    print(f"Saved {os.path.basename(filename)}: {len(sub_mesh.vertices)} verts, {len(sub_mesh.faces)} faces")

models_dir = r'C:\Projects\AR_Anatomy\Assets\_Project\Models'

print("Loading FBX geometries...")
vgeoms = parse_all_geometries(os.path.join(models_dir, 'VisceralSystem100.fbx'))
lgeoms = parse_all_geometries(os.path.join(models_dir, 'LymphoidOrgans100.fbx'))

# ==========================================
# 1. INTESTINE (Full system with 7 segments)
# ==========================================
print("\n--- Processing Intestine ---")
int_parts = {
    'intestine_duodenum.obj': vgeoms['Duodenum'],
    'intestine_jejunum.obj': vgeoms['Jejunum'],
    'intestine_ascending_colon.obj': vgeoms['Ascending colon.001'],
    'intestine_transverse_colon.obj': vgeoms['Transverse colon.001'],
    'intestine_descending_colon.obj': vgeoms['Descending colon.001'],
    'intestine_sigmoid_colon.obj': vgeoms['Sigmoid colon.001'],
    'intestine_appendix.obj': vgeoms['Vermiform appendix'],
}

# calculate common bounding box center so all parts align perfectly
all_int_verts = []
for name, (v, ind) in int_parts.items():
    m = build_trimesh(v, ind)
    all_int_verts.append(m.vertices)
int_combined_verts = np.vstack(all_int_verts)
int_center = (np.min(int_combined_verts, axis=0) + np.max(int_combined_verts, axis=0)) / 2.0

for name, (v, ind) in int_parts.items():
    m = build_trimesh(v, ind)
    m.vertices -= int_center
    m.export(os.path.join(models_dir, name))
    print(f"Saved {name}: {len(m.vertices)} verts")

# ==========================================
# 2. PANCREAS (Head, Body, Tail)
# ==========================================
print("\n--- Processing Pancreas ---")
pan_v, pan_ind = vgeoms['Pancreas']
m_pan = build_trimesh(pan_v, pan_ind)
pan_center = (np.min(m_pan.vertices, axis=0) + np.max(m_pan.vertices, axis=0)) / 2.0
m_pan.vertices -= pan_center

# In pancreas, X axis runs horizontally: Left is head (negative or positive), Right is tail
min_x, max_x = np.min(m_pan.vertices[:, 0]), np.max(m_pan.vertices[:, 0])
x_len = max_x - min_x
p1 = min_x + x_len * 0.38
p2 = min_x + x_len * 0.70

mask_head = m_pan.vertices[:, 0] <= p1
mask_body = (m_pan.vertices[:, 0] > p1) & (m_pan.vertices[:, 0] <= p2)
mask_tail = m_pan.vertices[:, 0] > p2

slice_mesh_by_mask(m_pan, mask_head, os.path.join(models_dir, 'pancreas_head.obj'))
slice_mesh_by_mask(m_pan, mask_body, os.path.join(models_dir, 'pancreas_body.obj'))
slice_mesh_by_mask(m_pan, mask_tail, os.path.join(models_dir, 'pancreas_tail.obj'))

# ==========================================
# 3. STOMACH (Fundus, Body, Pylorus)
# ==========================================
print("\n--- Processing Stomach ---")
st_v, st_ind = vgeoms['Stomach.001']
m_st = build_trimesh(st_v, st_ind)
st_center = (np.min(m_st.vertices, axis=0) + np.max(m_st.vertices, axis=0)) / 2.0
m_st.vertices -= st_center

min_y, max_y = np.min(m_st.vertices[:, 1]), np.max(m_st.vertices[:, 1])
y_len = max_y - min_y
y_fundus = min_y + y_len * 0.65
y_pylorus = min_y + y_len * 0.28

mask_fundus = m_st.vertices[:, 1] >= y_fundus
mask_body = (m_st.vertices[:, 1] < y_fundus) & (m_st.vertices[:, 1] >= y_pylorus)
mask_pylorus = m_st.vertices[:, 1] < y_pylorus

slice_mesh_by_mask(m_st, mask_fundus, os.path.join(models_dir, 'stomach_fundus.obj'))
slice_mesh_by_mask(m_st, mask_body, os.path.join(models_dir, 'stomach_body.obj'))
slice_mesh_by_mask(m_st, mask_pylorus, os.path.join(models_dir, 'stomach_pylorus.obj'))

# ==========================================
# 4. URINARY BLADDER (Apex, Body, Fundus)
# ==========================================
print("\n--- Processing Bladder ---")
bl_v, bl_ind = vgeoms['Urinary bladder.001']
m_bl = build_trimesh(bl_v, bl_ind)
bl_center = (np.min(m_bl.vertices, axis=0) + np.max(m_bl.vertices, axis=0)) / 2.0
m_bl.vertices -= bl_center

min_y, max_y = np.min(m_bl.vertices[:, 1]), np.max(m_bl.vertices[:, 1])
y_len = max_y - min_y
y1 = min_y + y_len * 0.35
y2 = min_y + y_len * 0.68

mask_fundus = m_bl.vertices[:, 1] <= y1
mask_body = (m_bl.vertices[:, 1] > y1) & (m_bl.vertices[:, 1] <= y2)
mask_apex = m_bl.vertices[:, 1] > y2

slice_mesh_by_mask(m_bl, mask_fundus, os.path.join(models_dir, 'bladder_fundus.obj'))
slice_mesh_by_mask(m_bl, mask_body, os.path.join(models_dir, 'bladder_body.obj'))
slice_mesh_by_mask(m_bl, mask_apex, os.path.join(models_dir, 'bladder_apex.obj'))

# ==========================================
# 5. THYROID GLAND (Right Lobe, Left Lobe, Isthmus, Parathyroid)
# ==========================================
print("\n--- Processing Thyroid ---")
th_v, th_ind = vgeoms['Thyroid gland.001']
m_th = build_trimesh(th_v, th_ind)
th_center = (np.min(m_th.vertices, axis=0) + np.max(m_th.vertices, axis=0)) / 2.0
m_th.vertices -= th_center

# Thyroid lobes are left/right along X, Isthmus is middle |X| < threshold
min_x, max_x = np.min(m_th.vertices[:, 0]), np.max(m_th.vertices[:, 0])
x_half = (max_x - min_x) * 0.16

mask_isthmus = np.abs(m_th.vertices[:, 0]) <= x_half
mask_right = m_th.vertices[:, 0] < -x_half
mask_left = m_th.vertices[:, 0] > x_half

slice_mesh_by_mask(m_th, mask_isthmus, os.path.join(models_dir, 'thyroid_isthmus.obj'))
slice_mesh_by_mask(m_th, mask_right, os.path.join(models_dir, 'thyroid_right_lobe.obj'))
slice_mesh_by_mask(m_th, mask_left, os.path.join(models_dir, 'thyroid_left_lobe.obj'))

# Parathyroid glands
para_keys = [k for k in vgeoms if 'parathyroid gland' in k.lower()]
para_meshes = []
for k in para_keys:
    pv, pi = vgeoms[k]
    pm = build_trimesh(pv, pi)
    pm.vertices -= th_center
    para_meshes.append(pm)
m_para = trimesh.util.concatenate(para_meshes)
m_para.export(os.path.join(models_dir, 'thyroid_parathyroid.obj'))
print(f"Saved thyroid_parathyroid.obj: {len(m_para.vertices)} verts")

# ==========================================
# 6. SPLEEN (Diaphragmatic, Visceral, Hilum)
# ==========================================
print("\n--- Processing Spleen ---")
sp_v, sp_ind = lgeoms['Spleen']
m_sp = build_trimesh(sp_v, sp_ind)
sp_center = (np.min(m_sp.vertices, axis=0) + np.max(m_sp.vertices, axis=0)) / 2.0
m_sp.vertices -= sp_center

# Spleen has convex lateral surface (diaphragmatic) and concave medial surface (visceral with hilum)
# Let's slice by Z/X
min_z, max_z = np.min(m_sp.vertices[:, 2]), np.max(m_sp.vertices[:, 2])
z_mid = (min_z + max_z) * 0.5
min_x, max_x = np.min(m_sp.vertices[:, 0]), np.max(m_sp.vertices[:, 0])
x_mid = (min_x + max_x) * 0.5

mask_diaph = m_sp.vertices[:, 2] <= z_mid
mask_hilum = (m_sp.vertices[:, 2] > z_mid) & (np.abs(m_sp.vertices[:, 0] - x_mid) < (max_x - min_x) * 0.25)
mask_visceral = (m_sp.vertices[:, 2] > z_mid) & (~mask_hilum)

slice_mesh_by_mask(m_sp, mask_diaph, os.path.join(models_dir, 'spleen_diaphragmatic.obj'))
slice_mesh_by_mask(m_sp, mask_visceral, os.path.join(models_dir, 'spleen_visceral.obj'))
slice_mesh_by_mask(m_sp, mask_hilum, os.path.join(models_dir, 'spleen_hilum.obj'))

# ==========================================
# 7. GALLBLADDER (Fundus, Body, Neck)
# ==========================================
print("\n--- Processing Gallbladder ---")
gb_path = os.path.join(models_dir, 'gallbladder.obj')
if os.path.exists(gb_path):
    m_gb = trimesh.load(gb_path)
else:
    gb_v, gb_ind = vgeoms['Gallbladder']
    m_gb = build_trimesh(gb_v, gb_ind)

gb_center = (np.min(m_gb.vertices, axis=0) + np.max(m_gb.vertices, axis=0)) / 2.0
m_gb.vertices -= gb_center

min_z, max_z = np.min(m_gb.vertices[:, 2]), np.max(m_gb.vertices[:, 2])
z_len = max_z - min_z
z1 = min_z + z_len * 0.38
z2 = min_z + z_len * 0.72

mask_fundus = m_gb.vertices[:, 2] <= z1
mask_body = (m_gb.vertices[:, 2] > z1) & (m_gb.vertices[:, 2] <= z2)
mask_neck = m_gb.vertices[:, 2] > z2

slice_mesh_by_mask(m_gb, mask_fundus, os.path.join(models_dir, 'gallbladder_fundus.obj'))
slice_mesh_by_mask(m_gb, mask_body, os.path.join(models_dir, 'gallbladder_body.obj'))
slice_mesh_by_mask(m_gb, mask_neck, os.path.join(models_dir, 'gallbladder_neck.obj'))

# ==========================================
# 8. UTERUS (Fundus, Body, Cervix, Tubes)
# ==========================================
print("\n--- Processing Uterus ---")
ut_path = os.path.join(models_dir, 'uterus.obj')
m_ut = trimesh.load(ut_path)
ut_center = (np.min(m_ut.vertices, axis=0) + np.max(m_ut.vertices, axis=0)) / 2.0
m_ut.vertices -= ut_center

min_y, max_y = np.min(m_ut.vertices[:, 1]), np.max(m_ut.vertices[:, 1])
y_len = max_y - min_y
min_x, max_x = np.min(m_ut.vertices[:, 0]), np.max(m_ut.vertices[:, 0])
x_half = (max_x - min_x) * 0.35

mask_tubes = np.abs(m_ut.vertices[:, 0]) > x_half
mask_cervix = (np.abs(m_ut.vertices[:, 0]) <= x_half) & (m_ut.vertices[:, 1] < min_y + y_len * 0.30)
mask_fundus = (np.abs(m_ut.vertices[:, 0]) <= x_half) & (m_ut.vertices[:, 1] > min_y + y_len * 0.70)
mask_body = (np.abs(m_ut.vertices[:, 0]) <= x_half) & (~mask_cervix) & (~mask_fundus)

slice_mesh_by_mask(m_ut, mask_fundus, os.path.join(models_dir, 'uterus_fundus.obj'))
slice_mesh_by_mask(m_ut, mask_body, os.path.join(models_dir, 'uterus_body.obj'))
slice_mesh_by_mask(m_ut, mask_cervix, os.path.join(models_dir, 'uterus_cervix.obj'))
slice_mesh_by_mask(m_ut, mask_tubes, os.path.join(models_dir, 'uterus_tubes.obj'))

print("\n--- ALL 12 ORGANS PROCESSED SUCCESSFULLY! ---")
