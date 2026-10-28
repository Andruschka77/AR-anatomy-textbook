import os, trimesh, numpy as np
from PIL import Image, ImageDraw, ImageFilter

models_dir = r'C:\Projects\AR_Anatomy\Assets\_Project\Models'

def generate_noise_texture(width, height, base_rgb, noise_rgb, grain=15, blur_radius=3):
    img = Image.new('RGB', (width, height), base_rgb)
    draw = ImageDraw.Draw(img)
    np.random.seed(42)
    # create organic mottled pattern
    arr = np.full((height, width, 3), base_rgb, dtype=np.float32)
    noise = np.random.normal(0, grain, (height, width, 3))
    arr = np.clip(arr + noise, 0, 255).astype(np.uint8)
    res = Image.fromarray(arr).filter(ImageFilter.GaussianBlur(blur_radius))
    return res

print("--- 1. Generating High-Res Anatomical Textures ---")

# Kidney Cortex (Reddish-brown with microgranulation)
tex_kidney_cortex = generate_noise_texture(1024, 1024, (145, 52, 60), (120, 38, 45), grain=22, blur_radius=2)
tex_kidney_cortex.save(os.path.join(models_dir, 'tex_kidney_cortex.png'))

# Kidney Pyramids (Deep crimson/purple with striations)
tex_kidney_pyramids = generate_noise_texture(1024, 1024, (110, 32, 48), (85, 20, 35), grain=18, blur_radius=2)
tex_kidney_pyramids.save(os.path.join(models_dir, 'tex_kidney_pyramids.png'))

# Kidney Pelvis (Ivory / fibrous white-pearl)
tex_kidney_pelvis = generate_noise_texture(1024, 1024, (225, 218, 195), (200, 190, 170), grain=12, blur_radius=2)
tex_kidney_pelvis.save(os.path.join(models_dir, 'tex_kidney_pelvis.png'))

# Intestine Small (Fresh warm salmon-pink mucosa)
tex_int_small = generate_noise_texture(1024, 1024, (220, 145, 130), (195, 120, 105), grain=16, blur_radius=2)
tex_int_small.save(os.path.join(models_dir, 'tex_int_small.png'))

# Intestine Large / Colon (Greyish-pink with haustra shading)
tex_int_large = generate_noise_texture(1024, 1024, (198, 138, 125), (170, 115, 102), grain=18, blur_radius=3)
tex_int_large.save(os.path.join(models_dir, 'tex_int_large.png'))

# Pancreas (Lobulated golden-amber glandular acini)
tex_pancreas = generate_noise_texture(1024, 1024, (232, 195, 115), (205, 168, 90), grain=25, blur_radius=2)
tex_pancreas.save(os.path.join(models_dir, 'tex_pancreas.png'))

# Spleen (Deep plum/wine red pulp with glistening capsule)
tex_spleen = generate_noise_texture(1024, 1024, (118, 36, 58), (92, 24, 42), grain=16, blur_radius=2)
tex_spleen.save(os.path.join(models_dir, 'tex_spleen.png'))

# Urinary Bladder (Warm amber-pink detrusor muscle)
tex_bladder = generate_noise_texture(1024, 1024, (222, 168, 108), (195, 142, 85), grain=18, blur_radius=3)
tex_bladder.save(os.path.join(models_dir, 'tex_bladder.png'))

# Gallbladder (Rich bile emerald-green)
tex_gallbladder = generate_noise_texture(1024, 1024, (35, 128, 62), (24, 102, 48), grain=15, blur_radius=2)
tex_gallbladder.save(os.path.join(models_dir, 'tex_gallbladder.png'))

# Thyroid (Rich coral vascular endocrine tissue)
tex_thyroid = generate_noise_texture(1024, 1024, (218, 105, 105), (188, 78, 78), grain=20, blur_radius=2)
tex_thyroid.save(os.path.join(models_dir, 'tex_thyroid.png'))

# Parathyroid (Golden-tan endocrine granules)
tex_parathyroid = generate_noise_texture(512, 512, (208, 164, 82), (180, 140, 62), grain=15, blur_radius=2)
tex_parathyroid.save(os.path.join(models_dir, 'tex_parathyroid.png'))

# Uterus (Deep velvety rose smooth muscle)
tex_uterus = generate_noise_texture(1024, 1024, (198, 92, 132), (168, 68, 105), grain=18, blur_radius=3)
tex_uterus.save(os.path.join(models_dir, 'tex_uterus.png'))

# Brain Lobe textures (Gyri/sulci shading)
tex_brain_frontal = generate_noise_texture(1024, 1024, (218, 110, 110), (185, 80, 80), grain=18, blur_radius=2)
tex_brain_frontal.save(os.path.join(models_dir, 'tex_brain_frontal.png'))
tex_brain_parietal = generate_noise_texture(1024, 1024, (228, 172, 95), (195, 142, 70), grain=18, blur_radius=2)
tex_brain_parietal.save(os.path.join(models_dir, 'tex_brain_parietal.png'))
tex_brain_occipital = generate_noise_texture(1024, 1024, (95, 175, 192), (68, 145, 162), grain=18, blur_radius=2)
tex_brain_occipital.save(os.path.join(models_dir, 'tex_brain_occipital.png'))
tex_brain_temporal = generate_noise_texture(1024, 1024, (168, 128, 212), (138, 98, 182), grain=18, blur_radius=2)
tex_brain_temporal.save(os.path.join(models_dir, 'tex_brain_temporal.png'))
tex_brain_cerebellum = generate_noise_texture(1024, 1024, (210, 142, 160), (178, 112, 130), grain=18, blur_radius=2)
tex_brain_cerebellum.save(os.path.join(models_dir, 'tex_brain_cerebellum.png'))
tex_brain_stem = generate_noise_texture(1024, 1024, (220, 210, 188), (190, 180, 158), grain=15, blur_radius=2)
tex_brain_stem.save(os.path.join(models_dir, 'tex_brain_stem.png'))

print("All procedural textures saved successfully.")

# Helper to assign cylindrical/spherical UVs and export
def export_with_uvs(mesh, filename, center_offset=None):
    new_v = mesh.vertices.copy()
    if center_offset is not None:
        new_v -= center_offset
    
    # Calculate UVs if not present
    x, y, z = new_v[:, 0], new_v[:, 1], new_v[:, 2]
    y_min, y_max = y.min(), y.max()
    y_range = max(y_max - y_min, 0.0001)
    
    u = 0.5 + np.arctan2(z, x) / (2.0 * np.pi)
    v = (y - y_min) / y_range
    
    sub = trimesh.Trimesh(vertices=new_v, faces=mesh.faces, process=True)
    sub.visual = trimesh.visual.TextureVisuals(uv=np.column_stack([u, v]))
    sub.export(filename)
    print(f"Exported {os.path.basename(filename)}: {len(sub.vertices)} verts, with UVs")

# ==========================================
# 2. LUNGS WITH TRACHEA (from lung.glb)
# ==========================================
print("\n--- 2. Processing Lungs with Trachea ---")
lung_glb_path = os.path.join(models_dir, 'lung.glb')
lung_scene = trimesh.load(lung_glb_path)
g_trachea = lung_scene.geometry['thairoid01:low_part01_Group6344:Group18522_thairoid01:lungh_part01_0']
g_lungs = lung_scene.geometry['thairoid01:low_part02_Group6344:Group24638_thairoid01:lungh_part02_0']

all_lung_v = np.vstack([g_trachea.vertices, g_lungs.vertices])
lung_center = (np.min(all_lung_v, axis=0) + np.max(all_lung_v, axis=0)) / 2.0

# Trachea & Bronchi
g_trachea.vertices -= lung_center
g_trachea.export(os.path.join(models_dir, 'lung_trachea.obj'))
print(f"Exported lung_trachea.obj: {len(g_trachea.vertices)} verts")

# Split Lungs into Right Lung and Left Lung
g_lungs.vertices -= lung_center
# X < 0 is Right lung (anatomical right), X >= 0 is Left lung
right_face_mask = np.mean(g_lungs.vertices[g_lungs.faces, 0], axis=1) < 0.0
left_face_mask = ~right_face_mask

def slice_by_face_mask(mesh, mask, out_path):
    sub_faces = mesh.faces[mask]
    used = np.unique(sub_faces)
    idx_map = np.full(len(mesh.vertices), -1, dtype=np.int32)
    idx_map[used] = np.arange(len(used))
    new_v = mesh.vertices[used]
    new_f = idx_map[sub_faces]
    new_uv = mesh.visual.uv[used] if hasattr(mesh.visual, 'uv') and mesh.visual.uv is not None else None
    res = trimesh.Trimesh(vertices=new_v, faces=new_f, process=True)
    if new_uv is not None:
        res.visual = trimesh.visual.TextureVisuals(uv=new_uv)
    res.export(out_path)
    print(f"Exported {os.path.basename(out_path)}: {len(res.vertices)} verts")

slice_by_face_mask(g_lungs, right_face_mask, os.path.join(models_dir, 'lung_right.obj'))
slice_by_face_mask(g_lungs, left_face_mask, os.path.join(models_dir, 'lung_left.obj'))

# ==========================================
# 3. LIVER (from liver.glb with native textures)
# ==========================================
print("\n--- 3. Processing Liver ---")
liv_scene = trimesh.load(os.path.join(models_dir, 'liver.glb'))
g_liv_body = liv_scene.geometry['liver.001_Material.011_0']
g_liv_gb = liv_scene.geometry['gallbladder.002_Material.013_0']
g_v1 = liv_scene.geometry['inferior vena cava.001_Material.012_0']
g_v2 = liv_scene.geometry['hepatic artery.001_Material_0']
g_v3 = liv_scene.geometry['portal vein.001_Material.015_0']
g_vessels = trimesh.util.concatenate([g_v1, g_v2, g_v3])

all_liv_v = np.vstack([g_liv_body.vertices, g_liv_gb.vertices, g_vessels.vertices])
liv_center = (np.min(all_liv_v, axis=0) + np.max(all_liv_v, axis=0)) / 2.0

g_liv_body.vertices -= liv_center
g_liv_gb.vertices -= liv_center
g_vessels.vertices -= liv_center

# Split Liver into Left and Right lobes along X
x_mid = 0.08
left_lobe_mask = np.mean(g_liv_body.vertices[g_liv_body.faces, 0], axis=1) > x_mid
right_lobe_mask = ~left_lobe_mask

slice_by_face_mask(g_liv_body, left_lobe_mask, os.path.join(models_dir, 'liver_left_lobe.obj'))
slice_by_face_mask(g_liv_body, right_lobe_mask, os.path.join(models_dir, 'liver_right_lobe.obj'))
g_liv_gb.export(os.path.join(models_dir, 'liver_gallbladder.obj'))
print(f"Exported liver_gallbladder.obj: {len(g_liv_gb.vertices)} verts")
g_vessels.export(os.path.join(models_dir, 'liver_vessels.obj'))
print(f"Exported liver_vessels.obj: {len(g_vessels.vertices)} verts")

# ==========================================
# 4. STOMACH (from stomach.glb with 4K texture)
# ==========================================
print("\n--- 4. Processing Stomach ---")
st_scene = trimesh.load(os.path.join(models_dir, 'stomach.glb'))
g_st = st_scene.geometry['Division_1_stomach_0']
st_center = (np.min(g_st.vertices, axis=0) + np.max(g_st.vertices, axis=0)) / 2.0
g_st.vertices -= st_center

y_min, y_max = g_st.vertices[:, 1].min(), g_st.vertices[:, 1].max()
y_len = y_max - y_min
y_pyl = y_min + y_len * 0.28
y_fun = y_min + y_len * 0.65

face_y = np.mean(g_st.vertices[g_st.faces, 1], axis=1)
mask_fun = face_y >= y_fun
mask_pyl = face_y < y_pyl
mask_body = (~mask_fun) & (~mask_pyl)

slice_by_face_mask(g_st, mask_fun, os.path.join(models_dir, 'stomach_fundus.obj'))
slice_by_face_mask(g_st, mask_body, os.path.join(models_dir, 'stomach_body.obj'))
slice_by_face_mask(g_st, mask_pyl, os.path.join(models_dir, 'stomach_pylorus.obj'))

# ==========================================
# 5. Adding UV coordinates to remaining organs
# ==========================================
print("\n--- 5. Adding UVs to remaining organ meshes ---")
organs_to_uv = [
    'kidney_cortex.obj', 'kidney_pyramids.obj', 'kidney_pelvis.obj', 'kidney_vessels.obj',
    'intestine_duodenum.obj', 'intestine_jejunum.obj', 'intestine_ascending_colon.obj',
    'intestine_transverse_colon.obj', 'intestine_descending_colon.obj', 'intestine_sigmoid_colon.obj',
    'intestine_appendix.obj',
    'pancreas_head.obj', 'pancreas_body.obj', 'pancreas_tail.obj',
    'spleen_diaphragmatic.obj', 'spleen_visceral.obj', 'spleen_hilum.obj',
    'bladder_apex.obj', 'bladder_body.obj', 'bladder_fundus.obj',
    'gallbladder_fundus.obj', 'gallbladder_body.obj', 'gallbladder_neck.obj',
    'thyroid_right_lobe.obj', 'thyroid_left_lobe.obj', 'thyroid_isthmus.obj', 'thyroid_parathyroid.obj',
    'uterus_fundus.obj', 'uterus_body.obj', 'uterus_cervix.obj', 'uterus_tubes.obj',
    'brain_Frontal_0.obj', 'brain_Parietal_0.obj', 'brain_Occipital_0.obj', 'brain_Temporal_0.obj',
    'brain_cerebellum_0.obj', 'brain_lambert1_0.obj'
]

for fname in organs_to_uv:
    p = os.path.join(models_dir, fname)
    if os.path.exists(p):
        m = trimesh.load(p)
        export_with_uvs(m, p)

print("\n--- ALL ASSETS AND TEXTURES GENERATED SUCCESSFULLY! ---")
