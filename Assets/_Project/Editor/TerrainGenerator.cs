using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PyroScan.EditorTools
{
    /// <summary>
    /// Génère le terrain 1 km x 1 km du Massif Sud (graine fixe) :
    /// vallée encaissée ouverte vers le nord, plateau du poste au sud, lac, route + pistes,
    /// textures (herbe, sol forestier, roche, terre) et forêt variée (conifères, feuillus, broussailles).
    /// Coordonnées monde : x de -500 à 500, z de -100 à 900, le poste de commandement est en (0, 0, 0).
    /// </summary>
    public static class TerrainGenerator
    {
        public const string Dir = "Assets/_Project/Terrain";
        public const int Seed = 20261114;
        public const float SizeXZ = 1000f, SizeY = 260f, BaseY = -150f, XStart = -500f, ZStart = -100f;
        public const float PlateauY = 0f, FloorY = -120f;
        const int HeightRes = 513, AlphaRes = 512;
        const float TreeSpacing = 8f; // ~25 % d'arbres en plus qu'avec 9 m

        static float ox, oz;

        /// <summary>Positions des éléments de la mission (en mètres, plan XZ).</summary>
        public class Layout
        {
            public readonly List<Vector2[]> roads = new List<Vector2[]>();
            public readonly List<float> roadWidths = new List<float>();
            public Vector2 posteCenter = new Vector2(5f, 5f);
            public float posteRadius = 45f;
            public Vector2 dronePad = new Vector2(12f, -1f);
            public Vector2 lakeCenter;
            public float lakeRadius = 55f;
            public float WaterY => FloorY - 1f;
            public Vector2 hamlet, camp, foyer, truckStart;
        }

        // ---------------------------------------------------------------- Disposition
        public static float AxisX(float z) => 30f * Mathf.Sin(z * 0.006f) + 15f * Mathf.Sin(z * 0.017f + 1f);

        public static Layout CreateLayout()
        {
            var L = new Layout();
            L.lakeCenter = new Vector2(AxisX(560f) - 40f, 560f);
            L.camp = new Vector2(L.lakeCenter.x - 10f, L.lakeCenter.y + 85f);
            L.hamlet = new Vector2(AxisX(780f) + 75f, 780f);
            L.foyer = new Vector2(-400f, 170f); // forêt du sud-ouest, ~600 m du campement dans le sens du vent
            L.truckStart = new Vector2(AxisX(300f) + 25f, 300f);

            // Route goudronnée : du plateau du poste, descente en lacets, puis remontée de la vallée.
            var main = new List<Vector2>
            {
                new Vector2(22f, 18f), new Vector2(40f, 60f), new Vector2(8f, 110f),
                new Vector2(50f, 160f), new Vector2(15f, 210f), new Vector2(AxisX(260f) + 25f, 260f)
            };
            for (float z = 300f; z <= 900f; z += 50f) main.Add(new Vector2(AxisX(z) + 25f, z));
            AddRoad(L, main.ToArray(), 8f);

            // Piste vers le campement (rive nord du lac).
            AddRoad(L, new[] { new Vector2(AxisX(620f) + 25f, 620f), new Vector2(AxisX(630f), 635f), L.camp }, 5f);
            // Boucle du hameau.
            AddRoad(L, new[]
            {
                new Vector2(AxisX(755f) + 25f, 755f), new Vector2(L.hamlet.x - 25f, 765f),
                new Vector2(L.hamlet.x + 20f, 790f), new Vector2(AxisX(805f) + 25f, 805f)
            }, 5f);
            // Piste de crête à l'est.
            AddRoad(L, new[]
            {
                new Vector2(AxisX(420f) + 25f, 420f), new Vector2(AxisX(430f) + 120f, 440f),
                new Vector2(AxisX(445f) + 220f, 470f), new Vector2(AxisX(455f) + 300f, 500f)
            }, 5f);
            // Piste forestière à l'ouest, vers le versant boisé.
            AddRoad(L, new[]
            {
                new Vector2(AxisX(360f) + 25f, 360f), new Vector2(AxisX(380f) - 60f, 390f),
                new Vector2(AxisX(410f) - 140f, 420f)
            }, 5f);
            return L;
        }

        static void AddRoad(Layout L, Vector2[] pts, float width)
        {
            // Densifie la polyligne (un point tous les ~10 m) pour un profil de route régulier.
            var dense = new List<Vector2>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(pts[i], pts[i + 1]) / 10f));
                for (int k = 0; k < n; k++) dense.Add(Vector2.Lerp(pts[i], pts[i + 1], k / (float)n));
            }
            dense.Add(pts[pts.Length - 1]);
            L.roads.Add(dense.ToArray());
            L.roadWidths.Add(width);
        }

        // ---------------------------------------------------------------- Génération
        public static Terrain Generate(Layout L)
        {
            SceneBuilder.EnsureFolder(Dir);
            var rng = new System.Random(Seed);
            ox = (float)rng.NextDouble() * 1000f;
            oz = (float)rng.NextDouble() * 1000f;

            EditorUtility.DisplayProgressBar("PyroScan", "Relief...", 0.1f);
            float[,] h = BuildHeights(L, out float[,] roadEdge);

            var td = new TerrainData();
            td.heightmapResolution = HeightRes;
            td.alphamapResolution = AlphaRes;
            td.baseMapResolution = 512;
            td.size = new Vector3(SizeXZ, SizeY, SizeXZ);

            var norm = new float[HeightRes, HeightRes];
            for (int j = 0; j < HeightRes; j++)
                for (int i = 0; i < HeightRes; i++)
                    norm[j, i] = Mathf.Clamp01((h[j, i] - BaseY) / SizeY);
            td.SetHeights(0, 0, norm);

            string tdPath = Dir + "/MassifSud_TerrainData.asset";
            AssetDatabase.DeleteAsset(tdPath);
            AssetDatabase.CreateAsset(td, tdPath);

            EditorUtility.DisplayProgressBar("PyroScan", "Textures du sol...", 0.4f);
            td.terrainLayers = new[]
            {
                Layer("Herbe_Seche", new Color(0.30f, 0.42f, 0.18f), 8f), // prairie verte (nom conservé pour garder les réglages)
                Layer("Sol_Forestier", new Color(0.15f, 0.21f, 0.10f), 6f),
                Layer("Roche", new Color(0.47f, 0.46f, 0.43f), 10f),
                Layer("Terre_Route", new Color(0.55f, 0.47f, 0.36f), 4f),
            };
            PaintAlphamaps(td, L, roadEdge);

            EditorUtility.DisplayProgressBar("PyroScan", "Forêt...", 0.6f);
            td.treePrototypes = new[]
            {
                new TreePrototype { prefab = TreePrefab("Arbre_Conifere", 0) },
                new TreePrototype { prefab = TreePrefab("Arbre_Feuillu", 1) },
                new TreePrototype { prefab = TreePrefab("Buisson", 2) },
            };
            PlaceTrees(td, L, roadEdge, rng);

            GameObject go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain Massif Sud (1 km)";
            go.transform.position = new Vector3(XStart, BaseY, ZStart);
            Terrain terrain = go.GetComponent<Terrain>();

            var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Terrain_Lit.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                AssetDatabase.CreateAsset(mat, Dir + "/Terrain_Lit.mat");
            }
            terrain.materialTemplate = mat;

            // Réglages pensés pour le Quest autonome.
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 400f;
            terrain.treeDistance = 400f;
            terrain.treeBillboardDistance = 2000f;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var frame = go.AddComponent<PyroScan.MapFrame>();
            frame.terrain = terrain;

            EditorUtility.SetDirty(td);
            return terrain;
        }

        static float Fbm(float x, float z, int octaves = 4)
        {
            float a = 0.5f, f = 1f, sum = 0f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += a * Mathf.PerlinNoise(x * f + ox, z * f + oz);
                norm += a;
                a *= 0.5f;
                f *= 2f;
            }
            return sum / norm;
        }

        static float SmoothMax(float a, float b, float k) => 0.5f * (a + b + Mathf.Sqrt((a - b) * (a - b) + k * k));

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>Densité de forêt (0 = clairière, 1 = forêt dense).</summary>
        public static float ForestMask(float x, float z) => Fbm(x * 0.004f + 13f, z * 0.004f + 7f, 3);

        static float BaseHeight(float x, float z)
        {
            float d = Mathf.Abs(x - AxisX(z));
            float t = Smooth(Mathf.InverseLerp(70f, 380f, d));
            float ridge = 15f + 45f * Mathf.PerlinNoise(z * 0.003f + ox, x > AxisX(z) ? 1.7f : 3.1f);
            float h = Mathf.Lerp(FloorY, ridge, t);

            // Fermeture sud : plateau du poste, qui descend vers la vallée.
            float south = Smooth(Mathf.InverseLerp(260f, 40f, z));
            h = SmoothMax(h, Mathf.Lerp(FloorY, PlateauY, south), 25f);

            h += (Fbm(x * 0.01f, z * 0.01f) - 0.5f) * Mathf.Lerp(3f, 28f, Mathf.Max(t, south * 0.5f));
            return Mathf.Max(h, FloorY - 0.5f);
        }

        static float[,] BuildHeights(Layout L, out float[,] roadEdge)
        {
            var h = new float[HeightRes, HeightRes];
            for (int j = 0; j < HeightRes; j++)
                for (int i = 0; i < HeightRes; i++)
                    h[j, i] = BaseHeight(CellX(i), CellZ(j));

            Flatten(h, L.posteCenter, L.posteRadius, 35f, PlateauY);
            Flatten(h, L.hamlet, 45f, 70f, Sample(h, L.hamlet));
            Flatten(h, L.lakeCenter, L.lakeRadius + 12f, 70f, FloorY);
            Flatten(h, L.camp, 25f, 45f, Sample(h, L.camp));

            // Cuvette du lac.
            for (int j = 0; j < HeightRes; j++)
                for (int i = 0; i < HeightRes; i++)
                {
                    float r = Vector2.Distance(new Vector2(CellX(i), CellZ(j)), L.lakeCenter);
                    if (r < L.lakeRadius)
                    {
                        float k = 1f - (r / L.lakeRadius) * (r / L.lakeRadius);
                        h[j, i] = Mathf.Min(h[j, i], FloorY - 1.5f - 7f * k);
                    }
                }

            // Routes : profil lissé, puis la route est creusée/remblayée dans le relief.
            roadEdge = new float[HeightRes, HeightRes];
            for (int j = 0; j < HeightRes; j++)
                for (int i = 0; i < HeightRes; i++)
                    roadEdge[j, i] = float.MaxValue;

            for (int r = 0; r < L.roads.Count; r++)
            {
                Vector2[] pts = L.roads[r];
                float half = L.roadWidths[r] * 0.5f;
                const float falloff = 8f;

                var prof = new float[pts.Length];
                for (int k = 0; k < pts.Length; k++) prof[k] = Sample(h, pts[k]);
                for (int pass = 0; pass < 4; pass++)
                    for (int k = 1; k < pts.Length - 1; k++)
                        prof[k] = (prof[k - 1] + prof[k] + prof[k + 1]) / 3f;

                for (int j = 0; j < HeightRes; j++)
                    for (int i = 0; i < HeightRes; i++)
                    {
                        var p = new Vector2(CellX(i), CellZ(j));
                        float best = float.MaxValue, bestH = 0f;
                        for (int k = 0; k < pts.Length - 1; k++)
                        {
                            Vector2 a = pts[k], b = pts[k + 1];
                            if (p.x < Mathf.Min(a.x, b.x) - 30f || p.x > Mathf.Max(a.x, b.x) + 30f ||
                                p.y < Mathf.Min(a.y, b.y) - 30f || p.y > Mathf.Max(a.y, b.y) + 30f) continue;
                            float d = SegDist(p, a, b, out float t);
                            if (d < best) { best = d; bestH = Mathf.Lerp(prof[k], prof[k + 1], t); }
                        }
                        if (best == float.MaxValue) continue;
                        float edge = best - half;
                        if (edge < roadEdge[j, i]) roadEdge[j, i] = edge;
                        if (edge < falloff)
                        {
                            float w = edge <= 0f ? 1f : Smooth(1f - edge / falloff);
                            h[j, i] = Mathf.Lerp(h[j, i], bestH, w);
                        }
                    }
            }
            return h;
        }

        static float CellX(int i) => XStart + i / (float)(HeightRes - 1) * SizeXZ;
        static float CellZ(int j) => ZStart + j / (float)(HeightRes - 1) * SizeXZ;

        static float Sample(float[,] h, Vector2 p)
        {
            float fx = Mathf.Clamp((p.x - XStart) / SizeXZ * (HeightRes - 1), 0f, HeightRes - 1.001f);
            float fz = Mathf.Clamp((p.y - ZStart) / SizeXZ * (HeightRes - 1), 0f, HeightRes - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = Mathf.Lerp(h[j, i], h[j, i + 1], tx);
            float b = Mathf.Lerp(h[j + 1, i], h[j + 1, i + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        static void Flatten(float[,] h, Vector2 c, float radius, float blend, float target)
        {
            for (int j = 0; j < HeightRes; j++)
                for (int i = 0; i < HeightRes; i++)
                {
                    float r = Vector2.Distance(new Vector2(CellX(i), CellZ(j)), c);
                    if (r > radius + blend) continue;
                    float w = r <= radius ? 1f : Smooth(1f - (r - radius) / blend);
                    h[j, i] = Mathf.Lerp(h[j, i], target, w);
                }
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            t = l2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        static float RoadEdgeAt(float[,] roadEdge, float x, float z)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt((x - XStart) / SizeXZ * (HeightRes - 1)), 0, HeightRes - 1);
            int j = Mathf.Clamp(Mathf.RoundToInt((z - ZStart) / SizeXZ * (HeightRes - 1)), 0, HeightRes - 1);
            return roadEdge[j, i];
        }

        // ---------------------------------------------------------------- Textures du sol
        static TerrainLayer Layer(string name, Color color, float tile)
        {
            string texPath = Dir + "/" + name + ".png";
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false); // alpha = brillance en URP : quasi nulle
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.09f + color.r * 50f, y * 0.09f) * 0.6f
                            + Mathf.PerlinNoise(x * 0.35f, y * 0.35f + color.g * 50f) * 0.4f;
                    Color px = color * Mathf.Lerp(0.8f, 1.15f, n);
                    px.a = 0.05f;
                    tex.SetPixel(x, y, px);
                }
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);

            string layerPath = Dir + "/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            layer.tileSize = new Vector2(tile, tile);
            layer.specular = Color.black;
            layer.smoothness = 0f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static void PaintAlphamaps(TerrainData td, Layout L, float[,] roadEdge)
        {
            var maps = new float[AlphaRes, AlphaRes, 4];
            for (int j = 0; j < AlphaRes; j++)
                for (int i = 0; i < AlphaRes; i++)
                {
                    float nx = (i + 0.5f) / AlphaRes, nz = (j + 0.5f) / AlphaRes;
                    float x = XStart + nx * SizeXZ, z = ZStart + nz * SizeXZ;
                    float steep = td.GetSteepness(nx, nz);
                    float edge = RoadEdgeAt(roadEdge, x, z);

                    float road = edge <= 0f ? 1f : Smooth(1f - edge / 2.5f);
                    if (Vector2.Distance(new Vector2(x, z), L.lakeCenter) < L.lakeRadius + 3f) road = 1f; // rives et fond du lac
                    float rock = Smooth(Mathf.InverseLerp(32f, 45f, steep));
                    float forest = Smooth(Mathf.InverseLerp(0.40f, 0.55f, ForestMask(x, z)));

                    float rest = 1f - road;
                    float rk = rest * rock;
                    float fo = (rest - rk) * forest;
                    float gr = rest - rk - fo;
                    maps[j, i, 0] = gr;
                    maps[j, i, 1] = fo;
                    maps[j, i, 2] = rk;
                    maps[j, i, 3] = road;
                }
            td.SetAlphamaps(0, 0, maps);
        }

        // ---------------------------------------------------------------- Forêt
        static void PlaceTrees(TerrainData td, Layout L, float[,] roadEdge, System.Random rng)
        {
            var trees = new List<TreeInstance>();
            for (float z = ZStart + 4f; z < ZStart + SizeXZ - 4f; z += TreeSpacing)
                for (float x = XStart + 4f; x < XStart + SizeXZ - 4f; x += TreeSpacing)
                {
                    float px = x + ((float)rng.NextDouble() - 0.5f) * TreeSpacing * 0.9f;
                    float pz = z + ((float)rng.NextDouble() - 0.5f) * TreeSpacing * 0.9f;
                    var p = new Vector2(px, pz);
                    float nx = (px - XStart) / SizeXZ, nz = (pz - ZStart) / SizeXZ;

                    if (RoadEdgeAt(roadEdge, px, pz) < 4f) continue;
                    if (Vector2.Distance(p, L.lakeCenter) < L.lakeRadius + 8f) continue;
                    if (Vector2.Distance(p, L.hamlet) < 65f) continue;
                    if (Vector2.Distance(p, L.camp) < 35f) continue;
                    if (Vector2.Distance(p, L.posteCenter) < 75f) continue;
                    if (td.GetSteepness(nx, nz) > 38f) continue;

                    float f = ForestMask(px, pz);
                    double roll = rng.NextDouble();
                    int proto;
                    if (f > 0.55f) { if (roll > 0.9) continue; proto = rng.NextDouble() < 0.6 ? 0 : 1; }
                    else if (f > 0.42f) { if (roll > 0.42) continue; proto = rng.NextDouble() < 0.5 ? 2 : (rng.NextDouble() < 0.5 ? 0 : 1); }
                    else { if (roll > 0.08) continue; proto = 2; }

                    float s = Mathf.Lerp(0.8f, 1.3f, (float)rng.NextDouble());
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(nx, 0f, nz),
                        widthScale = s,
                        heightScale = s * Mathf.Lerp(0.9f, 1.15f, (float)rng.NextDouble()),
                        rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                        color = Color.white,
                        lightmapColor = Color.white,
                        prototypeIndex = proto
                    });
                }
            td.SetTreeInstances(trees.ToArray(), true);
            Debug.Log($"[PyroScan] Forêt : {trees.Count} arbres et buissons.");
        }

        /// <summary>Arbre low-poly (tronc + feuillage), 2 sous-maillages, adapté au Quest.</summary>
        static GameObject TreePrefab(string name, int kind)
        {
            var v = new List<Vector3>();
            var trunk = new List<int>();
            var leaves = new List<int>();
            const int n = 7;

            if (kind == 0)       // conifère
            {
                AddCylinder(v, trunk, 0.35f, 0f, 3f, n);
                AddCone(v, leaves, 2.8f, 2f, 9f, n);
                AddCone(v, leaves, 2.0f, 5f, 12f, n);
            }
            else if (kind == 1)  // feuillu
            {
                AddCylinder(v, trunk, 0.4f, 0f, 3.5f, n);
                AddDiamond(v, leaves, 3.2f, 3f, 6f, 10f, n);
            }
            else                 // buisson
            {
                AddCylinder(v, trunk, 0.15f, 0f, 0.5f, 5);
                AddDiamond(v, leaves, 1.6f, 0.2f, 1.1f, 2.4f, n);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trunk, 0);
            mesh.SetTriangles(leaves, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string meshPath = Dir + "/" + name + "_Mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            Material trunkMat = SceneBuilder.Mat("Tronc", new Color(0.3f, 0.2f, 0.12f));
            Color leafColor = kind == 0 ? new Color(0.09f, 0.24f, 0.10f)
                            : kind == 1 ? new Color(0.18f, 0.38f, 0.12f)
                            : new Color(0.24f, 0.38f, 0.14f);
            Material leafMat = SceneBuilder.Mat("Feuillage_" + name, leafColor);
            trunkMat.enableInstancing = true;
            leafMat.enableInstancing = true;
            EditorUtility.SetDirty(trunkMat);
            EditorUtility.SetDirty(leafMat);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { trunkMat, leafMat };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Un LODGroup évite l'avertissement "Nature/Soft Occlusion" et coupe les arbres trop petits à l'écran.
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.01f, new Renderer[] { mr }) });
            string prefabPath = Dir + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void AddCylinder(List<Vector3> v, List<int> t, float r, float y0, float y1, int n)
        {
            int b = v.Count;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v.Add(new Vector3(Mathf.Cos(a) * r, y0, Mathf.Sin(a) * r));
                v.Add(new Vector3(Mathf.Cos(a) * r, y1, Mathf.Sin(a) * r));
            }
            for (int i = 0; i < n; i++)
            {
                int i0 = b + 2 * i, i1 = b + 2 * ((i + 1) % n);
                t.AddRange(new[] { i0, i0 + 1, i1 + 1, i0, i1 + 1, i1 });
            }
        }

        static void AddCone(List<Vector3> v, List<int> t, float r, float y0, float yApex, int n)
        {
            int b = v.Count;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v.Add(new Vector3(Mathf.Cos(a) * r, y0, Mathf.Sin(a) * r));
            }
            int apex = v.Count;
            v.Add(new Vector3(0f, yApex, 0f));
            int center = v.Count;
            v.Add(new Vector3(0f, y0, 0f));
            for (int i = 0; i < n; i++)
            {
                int i0 = b + i, i1 = b + (i + 1) % n;
                t.AddRange(new[] { i0, apex, i1 });     // flancs
                t.AddRange(new[] { i0, i1, center });   // dessous
            }
        }

        static void AddDiamond(List<Vector3> v, List<int> t, float r, float yBottom, float yMid, float yTop, int n)
        {
            int b = v.Count;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v.Add(new Vector3(Mathf.Cos(a) * r, yMid, Mathf.Sin(a) * r));
            }
            int top = v.Count;
            v.Add(new Vector3(0f, yTop, 0f));
            int bottom = v.Count;
            v.Add(new Vector3(0f, yBottom, 0f));
            for (int i = 0; i < n; i++)
            {
                int i0 = b + i, i1 = b + (i + 1) % n;
                t.AddRange(new[] { i0, top, i1 });
                t.AddRange(new[] { i0, i1, bottom });
            }
        }

        // ---------------------------------------------------------------- Combustible (étape 5)
        /// <summary>
        /// Combustible de chaque case de la grille du feu (n x n) : 0 aucun (lac, route, roche très raide),
        /// 1 herbe, 2 broussailles, 3 forêt. Calculé avec le même masque de végétation que la forêt.
        /// </summary>
        public static byte[] ComputeFuel(Layout L, Terrain terrain, int n)
        {
            var fuel = new byte[n * n];
            float cell = SizeXZ / n;
            TerrainData td = terrain.terrainData;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = XStart + (i + 0.5f) * cell, z = ZStart + (j + 0.5f) * cell;
                    var p = new Vector2(x, z);
                    byte f;
                    if (Vector2.Distance(p, L.lakeCenter) < L.lakeRadius) f = 0;
                    else if (OnRoad(L, p, 1.5f)) f = 0;
                    else if (td.GetSteepness((i + 0.5f) / n, (j + 0.5f) / n) > 42f) f = 0;
                    else
                    {
                        float m = ForestMask(x, z);
                        f = (byte)(m > 0.55f ? 3 : m > 0.42f ? 2 : 1);
                    }
                    fuel[j * n + i] = f;
                }
            return fuel;
        }

        static bool OnRoad(Layout L, Vector2 p, float margin)
        {
            for (int r = 0; r < L.roads.Count; r++)
            {
                Vector2[] pts = L.roads[r];
                float half = L.roadWidths[r] * 0.5f + margin;
                for (int k = 0; k < pts.Length - 1; k++)
                    if (SegDist(p, pts[k], pts[k + 1], out _) < half) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- Vue satellite
        /// <summary>
        /// Photographie le terrain vu du dessus (nord en haut) et l'enregistre en PNG.
        /// Utilisée par la table (étape 1), la carte murale (étape 2) et la minimap (étape 3).
        /// </summary>
        public static Texture2D CaptureSatellite(Terrain terrain, string path, int res = 2048)
        {
            bool fog = RenderSettings.fog;
            Color amb = RenderSettings.ambientLight;
            float treeDist = terrain.treeDistance, baseDist = terrain.basemapDistance, pixErr = terrain.heightmapPixelError;
            Light sun = null;
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            Quaternion sunRot = sun != null ? sun.transform.rotation : Quaternion.identity;
            float sunInt = sun != null ? sun.intensity : 1f;
            Color sunCol = sun != null ? sun.color : Color.white;

            RenderSettings.fog = false;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            terrain.treeDistance = 5000f;
            terrain.basemapDistance = 5000f;
            terrain.heightmapPixelError = 1f;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
                sun.intensity = 1.1f;
                sun.color = new Color(1f, 0.96f, 0.9f);
            }

            var camGo = new GameObject("Camera satellite (temporaire)");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = SizeXZ * 0.5f;
            cam.aspect = 1f;
            Vector3 c = terrain.transform.position + new Vector3(SizeXZ * 0.5f, 0f, SizeXZ * 0.5f);
            camGo.transform.position = new Vector3(c.x, 700f, c.z);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // regarde vers le bas, nord (+Z) en haut
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.3f, 0.3f, 0.3f);

            var rt = new RenderTexture(res, res, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);

            RenderSettings.fog = fog;
            RenderSettings.ambientLight = amb;
            terrain.treeDistance = treeDist;
            terrain.basemapDistance = baseDist;
            terrain.heightmapPixelError = pixErr;
            if (sun != null)
            {
                sun.transform.rotation = sunRot;
                sun.intensity = sunInt;
                sun.color = sunCol;
            }

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = res;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
