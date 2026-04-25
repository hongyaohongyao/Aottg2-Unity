using System.Collections.Generic;
using UnityEngine;
using Map;
using Utility;

namespace MapEditor
{
    public enum BrushMode
    {
        Scatter = 0,
        Grid = 1,
    }

    public class BrushEntry
    {
        public string AssetName;
        public float Weight = 1f;
        public bool Advanced = false;
        // Simple mode
        public float ScaleMin = 1f;
        public float ScaleMax = 1f;
        public float YawMin = 0f;
        public float YawMax = 360f;
        // Advanced mode (per-axis)
        public float ScaleXMin = 1f;
        public float ScaleXMax = 1f;
        public float ScaleYMin = 1f;
        public float ScaleYMax = 1f;
        public float ScaleZMin = 1f;
        public float ScaleZMax = 1f;
        public float RotXMin = 0f;
        public float RotXMax = 0f;
        public float RotYMin = 0f;
        public float RotYMax = 360f;
        public float RotZMin = 0f;
        public float RotZMax = 0f;
        // Inspector-style overrides applied to placed objects.
        public bool Active = true;
        public bool Static = true;
        public bool Visible = true;
        public string CollideMode = MapObjectCollideMode.Physical;
        public string CollideWith = MapObjectCollideWith.Entities;
        public string PhysicsMaterial = "Default";
        public string Shader = MapObjectShader.Default;
        public Color255 Color = new Color255();
        public Color255 ReflectColor = new Color255();
        public string Texture = "Misc/None";
        public Vector2 Tiling = Vector2.one;
        public Vector2 Offset = Vector2.zero;
        // Vertical offset applied after ground projection; negative values clip
        // the placement into the surface beneath it.
        public float YOffset = 0f;
    }

    // Simple brush placement tool used by the Map Editor.
    // Owns a circle indicator plus translucent ghost previews that are
    // laid out deterministically from a seed so the preview doesn't jitter.
    public class MapEditorBrush
    {
        public bool Active;
        public bool AssetSelectMode;
        public BrushMode Mode = BrushMode.Scatter;
        public float Radius = 10f;
        public int Count = 8;
        public bool RandomizeEachPlacement = true;
        // Grid-mode state.
        public int GridX = 3;
        public int GridY = 1;
        public int GridZ = 3;
        public float SpacingX = 5f;
        public float SpacingY = 5f;
        public float SpacingZ = 5f;
        // 0 = none, 1 = X, 2 = Y, 3 = Z. Selects which grid axis is curved.
        public int CurveAxis = 0;
        public float CurveAmount = 0f;
        public List<BrushEntry> Entries = new List<BrushEntry>();

        private GameObject _circleIndicator;
        private LineRenderer _circleRenderer;
        private GameObject _diskIndicator;
        private MeshFilter _diskFilter;
        private Mesh _diskMesh;
        private List<GameObject> _ghostObjects = new List<GameObject>();
        private List<string> _ghostAssets = new List<string>();
        private List<Vector3> _ghostScales = new List<Vector3>();
        private List<Vector3> _ghostRotations = new List<Vector3>();
        private List<Vector3> _ghostLocalOffsets = new List<Vector3>();
        private int _ghostIdCounter = -100000;
        private Vector3 _center;
        private bool _hasCenter;
        private int _seed = 12345;

        public void SetActive(bool active)
        {
            Active = active;
            AssetSelectMode = false;
            if (!active)
            {
                ClearGhosts();
                DestroyCircle();
                _hasCenter = false;
            }
            else
            {
                EnsureCircle();
                RegenerateGhosts();
            }
        }

        public void AddEntry(string asset)
        {
            if (!BuiltinMapPrefabs.AllPrefabs.ContainsKey(asset) && !asset.StartsWith("Custom/"))
                return;
            foreach (var e in Entries)
                if (e.AssetName == asset) return;
            Entries.Add(new BrushEntry { AssetName = asset });
            RegenerateGhosts();
        }

        public void RemoveEntry(int index)
        {
            if (index < 0 || index >= Entries.Count) return;
            Entries.RemoveAt(index);
            RegenerateGhosts();
        }

        public void SetRadius(float radius)
        {
            Radius = Mathf.Clamp(radius, 0.5f, 500f);
            UpdateCircle();
            PositionGhosts();
        }

        public void SetCount(int count)
        {
            Count = Mathf.Clamp(count, 1, 200);
            RegenerateGhosts();
        }

        public void Reseed()
        {
            _seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        public void SetCenter(Vector3 center)
        {
            _center = center;
            _hasCenter = true;
            UpdateCircle();
            PositionGhosts();
        }

        public void ClearCenter()
        {
            _hasCenter = false;
            if (_circleRenderer != null)
                _circleRenderer.positionCount = 0;
            if (_diskMesh != null)
                _diskMesh.Clear();
            foreach (var go in _ghostObjects)
                if (go != null) go.SetActive(false);
        }

        private void EnsureCircle()
        {
            if (_circleIndicator != null) return;
            _circleIndicator = new GameObject("MapEditorBrushCircle");
            _circleRenderer = _circleIndicator.AddComponent<LineRenderer>();
            _circleRenderer.loop = true;
            _circleRenderer.useWorldSpace = true;
            _circleRenderer.widthMultiplier = 0.2f;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            _circleRenderer.material = mat;
            _circleRenderer.startColor = mat.color;
            _circleRenderer.endColor = mat.color;
            _circleRenderer.positionCount = 0;

            _diskIndicator = new GameObject("MapEditorBrushDisk");
            _diskFilter = _diskIndicator.AddComponent<MeshFilter>();
            var mr = _diskIndicator.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var diskMat = new Material(shader);
            diskMat.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            diskMat.SetOverrideTag("RenderType", "Transparent");
            if (diskMat.HasProperty("_SrcBlend")) diskMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (diskMat.HasProperty("_DstBlend")) diskMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (diskMat.HasProperty("_ZWrite")) diskMat.SetInt("_ZWrite", 0);
            diskMat.renderQueue = 3000;
            mr.sharedMaterial = diskMat;
            _diskMesh = new Mesh { name = "BrushDisk" };
            _diskMesh.MarkDynamic();
            _diskFilter.mesh = _diskMesh;
        }

        private void DestroyCircle()
        {
            if (_circleIndicator != null)
            {
                Object.Destroy(_circleIndicator);
                _circleIndicator = null;
                _circleRenderer = null;
            }
            if (_diskIndicator != null)
            {
                Object.Destroy(_diskIndicator);
                _diskIndicator = null;
                _diskFilter = null;
                _diskMesh = null;
            }
        }

        private void UpdateCircle()
        {
            if (_circleRenderer == null || !_hasCenter) return;
            // Grid mode hides the brush circle/disk; only ghosts are shown.
            if (Mode == BrushMode.Grid)
            {
                _circleRenderer.positionCount = 0;
                if (_diskMesh != null) _diskMesh.Clear();
                return;
            }
            const int segments = 96;
            int mask = PhysicsLayer.GetMask(PhysicsLayer.MapEditorObject);
            _circleRenderer.positionCount = segments;
            var rim = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float x = _center.x + Mathf.Cos(a) * Radius;
                float z = _center.z + Mathf.Sin(a) * Radius;
                float y = _center.y;
                RaycastHit hit;
                Vector3 origin = new Vector3(x, _center.y + 1000f, z);
                if (Physics.Raycast(origin, Vector3.down, out hit, 5000f, mask))
                    y = hit.point.y;
                rim[i] = new Vector3(x, y + 0.1f, z);
                _circleRenderer.SetPosition(i, rim[i]);
            }
            UpdateDisk(rim, mask);
        }

        private void UpdateDisk(Vector3[] rim, int mask)
        {
            if (_diskMesh == null) return;
            int segments = rim.Length;
            // Center ground height
            float cy = _center.y;
            RaycastHit chit;
            if (Physics.Raycast(new Vector3(_center.x, _center.y + 1000f, _center.z), Vector3.down, out chit, 5000f, mask))
                cy = chit.point.y;
            var verts = new Vector3[segments + 1];
            verts[0] = new Vector3(_center.x, cy + 0.05f, _center.z);
            for (int i = 0; i < segments; i++)
                verts[i + 1] = rim[i];
            var tris = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = next + 1;
            }
            _diskMesh.Clear();
            _diskMesh.vertices = verts;
            _diskMesh.triangles = tris;
            _diskMesh.RecalculateBounds();
        }

        public void RegenerateGhosts()
        {
            ClearGhosts();
            if (!Active || Entries.Count == 0) return;
            var rand = new System.Random(_seed);
            float totalWeight = 0f;
            foreach (var e in Entries) totalWeight += Mathf.Max(0.0001f, e.Weight);
            int total = Mode == BrushMode.Grid
                ? Mathf.Max(1, GridX) * Mathf.Max(1, GridY) * Mathf.Max(1, GridZ)
                : Count;
            int gx = Mathf.Max(1, GridX), gy = Mathf.Max(1, GridY), gz = Mathf.Max(1, GridZ);
            for (int n = 0; n < total; n++)
            {
                var entry = PickEntry(rand, totalWeight);
                Vector3 scale;
                Vector3 rot;
                if (entry.Advanced)
                {
                    scale = new Vector3(
                        Mathf.Lerp(entry.ScaleXMin, entry.ScaleXMax, (float)rand.NextDouble()),
                        Mathf.Lerp(entry.ScaleYMin, entry.ScaleYMax, (float)rand.NextDouble()),
                        Mathf.Lerp(entry.ScaleZMin, entry.ScaleZMax, (float)rand.NextDouble()));
                    rot = new Vector3(
                        Mathf.Lerp(entry.RotXMin, entry.RotXMax, (float)rand.NextDouble()),
                        Mathf.Lerp(entry.RotYMin, entry.RotYMax, (float)rand.NextDouble()),
                        Mathf.Lerp(entry.RotZMin, entry.RotZMax, (float)rand.NextDouble()));
                }
                else
                {
                    float s = Mathf.Lerp(entry.ScaleMin, entry.ScaleMax, (float)rand.NextDouble());
                    scale = new Vector3(s, s, s);
                    rot = new Vector3(0f, Mathf.Lerp(entry.YawMin, entry.YawMax, (float)rand.NextDouble()), 0f);
                }
                var script = CloneScript(entry.AssetName);
                if (script == null) continue;
                script.ScaleX *= scale.x;
                script.ScaleY *= scale.y;
                script.ScaleZ *= scale.z;
                script.RotationX = rot.x;
                script.RotationY = rot.y;
                script.RotationZ = rot.z;
                script.Id = _ghostIdCounter--;
                ApplyEntryFields(script, entry);
                script.CollideMode = MapObjectCollideMode.None;
                script.Networked = false;
                var mapObj = MapLoader.LoadObject(script, true);
                MapLoader.IdToMapObject.Remove(script.Id);
                MapLoader.GoToMapObject.Remove(mapObj.GameObject);
                if (MapLoader.IdToChildren.ContainsKey(script.Parent))
                    MapLoader.IdToChildren[script.Parent].Remove(script.Id);
                ApplyGhostAppearance(mapObj.GameObject);
                _ghostObjects.Add(mapObj.GameObject);
                _ghostAssets.Add(entry.AssetName);
                _ghostScales.Add(scale);
                _ghostRotations.Add(rot);
                if (Mode == BrushMode.Grid)
                {
                    int i = n % gx;
                    int j = (n / gx) % gy;
                    int k = n / (gx * gy);
                    Vector3 local = new Vector3(
                        (i - (gx - 1) * 0.5f) * SpacingX,
                        (j - (gy - 1) * 0.5f) * SpacingY,
                        (k - (gz - 1) * 0.5f) * SpacingZ);
                    _ghostLocalOffsets.Add(local);
                }
                else
                {
                    _ghostLocalOffsets.Add(Vector3.zero);
                }
            }
            if (_hasCenter) PositionGhosts();
            else foreach (var go in _ghostObjects) if (go != null) go.SetActive(false);
        }

        private BrushEntry PickEntry(System.Random rand, float totalWeight)
        {
            float r = (float)rand.NextDouble() * totalWeight;
            foreach (var e in Entries)
            {
                r -= Mathf.Max(0.0001f, e.Weight);
                if (r <= 0f) return e;
            }
            return Entries[Entries.Count - 1];
        }

        private MapScriptSceneObject CloneScript(string asset)
        {
            if (asset.StartsWith("Custom/"))
            {
                var parts = asset.Split('/');
                var s = new MapScriptSceneObject();
                s.Asset = asset;
                s.Name = parts.Length > 2 ? parts[2] : asset;
                s.Material.Shader = MapObjectShader.DefaultNoTint;
                return s;
            }
            if (!BuiltinMapPrefabs.AllPrefabs.ContainsKey(asset))
                return null;
            var prefab = (MapScriptSceneObject)BuiltinMapPrefabs.AllPrefabs[asset];
            var objs = new MapScriptObjects();
            objs.Objects.Add(prefab);
            var csv = objs.Serialize();
            objs = new MapScriptObjects();
            objs.Deserialize(csv);
            return (MapScriptSceneObject)objs.Objects[0];
        }

        private void ClearGhosts()
        {
            foreach (var go in _ghostObjects)
                if (go != null) Object.Destroy(go);
            _ghostObjects.Clear();
            _ghostAssets.Clear();
            _ghostScales.Clear();
            _ghostRotations.Clear();
            _ghostLocalOffsets.Clear();
        }

        private void ApplyGhostAppearance(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>())
                col.enabled = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                var newMats = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var m = new Material(mats[i]);
                    TryMakeTransparent(m);
                    if (m.HasProperty("_Color"))
                    {
                        var baseColor = m.color;
                        m.color = new Color(
                            Mathf.Lerp(baseColor.r, 0.6f, 0.5f),
                            Mathf.Lerp(baseColor.g, 0.9f, 0.5f),
                            Mathf.Lerp(baseColor.b, 1.0f, 0.5f),
                            0.45f);
                    }
                    newMats[i] = m;
                }
                r.sharedMaterials = newMats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void TryMakeTransparent(Material m)
        {
            // Best-effort: switch common built-in/URP opaque materials to fade/transparent so alpha shows through.
            if (m.HasProperty("_Mode"))
                m.SetFloat("_Mode", 2f); // Fade
            if (m.HasProperty("_Surface"))
                m.SetFloat("_Surface", 1f); // Transparent (URP)
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_SrcBlend"))
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend"))
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite"))
                m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        private void PositionGhosts()
        {
            if (!_hasCenter)
            {
                foreach (var go in _ghostObjects) if (go != null) go.SetActive(false);
                return;
            }
            var rand = new System.Random(_seed + 1);
            int ghostMask = PhysicsLayer.GetMask(PhysicsLayer.MapEditorObject);
            for (int i = 0; i < _ghostObjects.Count; i++)
            {
                var go = _ghostObjects[i];
                if (go == null) continue;
                if (!go.activeSelf) go.SetActive(true);
                Vector3 worldPos;
                Vector3 local = Vector3.zero;
                Quaternion tangent = Quaternion.identity;
                if (Mode == BrushMode.Grid)
                {
                    local = i < _ghostLocalOffsets.Count ? _ghostLocalOffsets[i] : Vector3.zero;
                    local = ApplyCurve(local, out tangent);
                    worldPos = _center + local;
                }
                else
                {
                    float radial = Mathf.Sqrt((float)rand.NextDouble()) * Radius;
                    float angle = (float)rand.NextDouble() * Mathf.PI * 2f;
                    worldPos = _center + new Vector3(Mathf.Cos(angle) * radial, 0f, Mathf.Sin(angle) * radial);
                }
                Quaternion baseRot = Quaternion.Euler(_ghostRotations[i]);
                go.transform.rotation = Mode == BrushMode.Grid ? tangent * baseRot : baseRot;
                // Bounds/bottom calc requires an up-to-date transform; temporarily lift high.
                go.transform.position = worldPos + Vector3.up * 500f;
                Vector3 projected = ProjectToGround(go, worldPos, ghostMask);
                // Grid mode: preserve vertical offsets (grid Y layer + curve Y bend)
                // that ProjectToGround would otherwise collapse onto the terrain.
                if (Mode == BrushMode.Grid)
                    projected.y += local.y;
                var entry = FindEntry(_ghostAssets[i]);
                if (entry != null)
                    projected.y += entry.YOffset;
                go.transform.position = projected;
            }
        }

        // Returns the average world-space bounding box size across all current
        // ghost objects. Used by input handlers to scale spacing steps to the
        // placed assets rather than using a tiny fixed increment.
        public Vector3 GetAverageGhostSize()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var go in _ghostObjects)
            {
                if (go == null) continue;
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                sum += b.size;
                count++;
            }
            if (count == 0) return Vector3.one;
            return sum / count;
        }

        private Vector3 ApplyCurve(Vector3 local)
        {
            Quaternion _;
            return ApplyCurve(local, out _);
        }

        // Bends the grid into a circular arc around the selected axis.
        // CurveAmount is interpreted as the total arc angle (degrees) spanned
        // across the longest non-curve axis of the grid. This lets the row form
        // a true arc, wrap into a full circle at 360°, and flip direction when
        // the amount is negative. Also outputs the tangent rotation so callers
        // can orient each ghost to face along the arc.
        private Vector3 ApplyCurve(Vector3 local, out Quaternion tangent)
        {
            tangent = Quaternion.identity;
            if (CurveAxis == 0 || Mathf.Abs(CurveAmount) < 0.0001f)
                return local;
            int gx = Mathf.Max(1, GridX), gy = Mathf.Max(1, GridY), gz = Mathf.Max(1, GridZ);
            Vector3 axisVec = CurveAxis == 1 ? Vector3.right : CurveAxis == 2 ? Vector3.up : Vector3.forward;
            // Pick the longest non-curve axis as the "row" axis along which the grid is bent.
            float lenX = CurveAxis == 1 ? 0f : (gx - 1) * SpacingX;
            float lenY = CurveAxis == 2 ? 0f : (gy - 1) * SpacingY;
            float lenZ = CurveAxis == 3 ? 0f : (gz - 1) * SpacingZ;
            Vector3 rowAxisVec;
            float s, L;
            if (lenX >= lenY && lenX >= lenZ) { rowAxisVec = Vector3.right; s = local.x; L = lenX; }
            else if (lenZ >= lenY) { rowAxisVec = Vector3.forward; s = local.z; L = lenZ; }
            else { rowAxisVec = Vector3.up; s = local.y; L = lenY; }
            if (L < 0.0001f) return local;
            Vector3 perp = Vector3.Cross(axisVec, rowAxisVec).normalized;
            float totalRad = CurveAmount * Mathf.Deg2Rad;
            float R = L / totalRad; // signed: negative CurveAmount bends the opposite way
            float theta = s / R;
            // Preserve any component of local along the other two axes (e.g. column offset
            // and grid-layer offset) by translating them rigidly with the bent row.
            Vector3 perpOffset = Vector3.Dot(local, perp) * perp;
            Vector3 axisOffset = Vector3.Dot(local, axisVec) * axisVec;
            Vector3 arcPos = rowAxisVec * (R * Mathf.Sin(theta)) + perp * (R * (1f - Mathf.Cos(theta)));
            tangent = Quaternion.AngleAxis(theta * Mathf.Rad2Deg, axisVec);
            return arcPos + perpOffset + axisOffset;
        }

        private Vector3 ProjectToGround(GameObject go, Vector3 xz, int mask)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            float bottomOffsetY = 0f;
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                bottomOffsetY = go.transform.position.y - b.min.y;
            }
            RaycastHit hit;
            if (Physics.Raycast(new Ray(new Vector3(xz.x, go.transform.position.y, xz.z), Vector3.down), out hit, 2000f, mask))
                return new Vector3(xz.x, hit.point.y + bottomOffsetY, xz.z);
            return new Vector3(xz.x, xz.y + bottomOffsetY, xz.z);
        }

        // Produce the concrete placement scripts at current ghost positions
        public List<MapScriptBaseObject> BuildPlacements()
        {
            var results = new List<MapScriptBaseObject>();
            for (int i = 0; i < _ghostObjects.Count; i++)
            {
                var go = _ghostObjects[i];
                if (go == null) continue;
                var entry = FindEntry(_ghostAssets[i]);
                var script = CloneScript(_ghostAssets[i]);
                if (script == null) continue;
                script.ScaleX *= _ghostScales[i].x;
                script.ScaleY *= _ghostScales[i].y;
                script.ScaleZ *= _ghostScales[i].z;
                script.SetPosition(go.transform.position);
                script.SetRotation(go.transform.rotation);
                if (entry != null)
                    ApplyEntryFields(script, entry);
                results.Add(script);
            }
            return results;
        }

        private BrushEntry FindEntry(string asset)
        {
            foreach (var e in Entries)
                if (e.AssetName == asset) return e;
            return null;
        }

        // Copies inspector-style fields from a BrushEntry onto a script, selecting
        // the proper material subclass based on the chosen shader (mirrors
        // MapEditorInspectPanel behavior).
        private static void ApplyEntryFields(MapScriptSceneObject script, BrushEntry entry)
        {
            script.Active = entry.Active;
            script.Static = entry.Static;
            script.Visible = entry.Visible;
            script.CollideMode = entry.CollideMode;
            script.CollideWith = entry.CollideWith;
            script.PhysicsMaterial = entry.PhysicsMaterial;

            if (script.Material == null || script.Material.Shader != entry.Shader)
            {
                if (entry.Shader == MapObjectShader.Default || entry.Shader == MapObjectShader.DefaultNoTint)
                    script.Material = new MapScriptBaseMaterial();
                else if (entry.Shader == MapObjectShader.DefaultTiled)
                    script.Material = new MapScriptDefaultTiledMaterial();
                else if (entry.Shader == MapObjectShader.Basic || entry.Shader == MapObjectShader.Transparent)
                    script.Material = new MapScriptBasicMaterial();
                else if (entry.Shader == MapObjectShader.Reflective)
                    script.Material = new MapScriptReflectiveMaterial();
                else if (MapObjectShader.IsLegacyShader(entry.Shader))
                    script.Material = new MapScriptLegacyMaterial();
                else
                    script.Material = new MapScriptBaseMaterial();
                script.Material.Shader = entry.Shader;
            }
            script.Material.Color = entry.Color;
            if (script.Material is MapScriptLegacyMaterial legacy)
                legacy.Tiling = entry.Tiling;
            else if (script.Material is MapScriptDefaultTiledMaterial tiled)
                tiled.Tiling = entry.Tiling;
            else if (script.Material is MapScriptBasicMaterial basic)
            {
                basic.Texture = entry.Texture;
                basic.Tiling = entry.Tiling;
                basic.Offset = entry.Offset;
                if (basic is MapScriptReflectiveMaterial refl)
                    refl.ReflectColor = entry.ReflectColor;
            }
        }
    }
}
