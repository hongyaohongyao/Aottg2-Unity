using System.Collections.Generic;
using UnityEngine;
using UI;
using Utility;
using CustomSkins;
using ApplicationManagers;
using Map;
using Settings;
using Cameras;

namespace MapEditor
{
    class PositionGizmo : BaseGizmo
    {
        private Transform _lineX;
        private Transform _lineY;
        private Transform _lineZ;
        // Plane handles created at runtime. Each corresponds to translating in 2 axes at once.
        private Transform _planeXY;
        private Transform _planeYZ;
        private Transform _planeXZ;
        // Center square used for free-move vertex snapping. Only visible while VertexSnap is held
        // (so users can pick it up to drag the selection anywhere on screen).
        private Transform _centerSquare;
        private Color SelectedColor = Color.white;
        private Color LineXColor = Color.red;
        private Color LineYColor = Color.yellow;
        private Color LineZColor = Color.blue;
        private Transform _activeLine;
        private bool _activeIsPlane;
        private bool _activeIsCenter;
        // Plane used for plane-drag. World-space plane captured at drag start.
        private Plane _dragPlane;
        private Vector3 _previousMousePoint;
        // Vertex snap state
        private bool _vertexSnapActive;
        private Vector3 _vertexSnapOrigin; // Selection pivot used for vertex-snap anchor (first selected object)
        // Locked anchor for the current center-square / free-drag session. Stored in the selected
        // object's local space so it continues to refer to the same vertex even as the object moves.
        private Transform _snapAnchorTransform;
        private Vector3 _snapAnchorLocal;
        private bool _hasSnapAnchor;
        // Axis-ray snap state: captured direction (+1/-1) along the active axis, determined from
        // the user's drag gesture, and the mouse position where the drag started.
        private int _axisSnapDirection;
        private Vector2 _axisSnapStartMouse;
        // Maximum screen-space distance (pixels) for snapping to a non-selected scene vertex.
        private const float VertexSnapScreenRadius = 30f;
        // When two candidate vertices project to within this many pixels of each other, prefer
        // the one closer to the camera. Prevents the back-face vertex from winning over the
        // visually-closer front-face vertex at overlapping screen positions.
        private const float VertexSnapDepthTiebreakPixels = 6f;
        // Maximum world-space distance (in units) between the anchor vertex and a candidate target
        // vertex, scaled by the camera's distance to the anchor. At typical editor framing this
        // works out to a snap radius roughly proportional to how "close" the user can visually aim.
        // e.g. 0.15 means "snap only if within 15% of the camera-to-anchor distance".
        private const float VertexSnapWorldRadiusFactor = 0.3f;
        // Lower bound on the dynamic world-space snap radius. When the camera is very close to the
        // anchor the factor-based radius can become too small to reach neighboring vertices; this
        // clamp keeps the snap usable regardless of zoom level.
        private const float VertexSnapWorldRadiusMin = 60f;


        public static PositionGizmo Create()
        {
            var go = ResourceManager.InstantiateAsset<GameObject>(ResourcePaths.Map, "Gizmos/PositionGizmo");
            var gizmo = go.AddComponent<PositionGizmo>();
            go.SetActive(false);
            return gizmo;
        }

        public override bool IsActive()
        {
            return _activeLine != null;
        }

        protected override void Awake()
        {
            base.Awake();
            _lineX = _transform.Find("LineX");
            _lineY = _transform.Find("LineY");
            _lineZ = _transform.Find("LineZ");
            CreatePlaneHandles();
            ResetColors();
        }

        // Build small square plane handles at the gizmo origin for dual-axis movement.
        private void CreatePlaneHandles()
        {
            _planeXY = CreatePlaneHandle("PlaneXY", new Vector3(1f, 1f, 0f), LineZColor);
            _planeYZ = CreatePlaneHandle("PlaneYZ", new Vector3(0f, 1f, 1f), LineXColor);
            _planeXZ = CreatePlaneHandle("PlaneXZ", new Vector3(1f, 0f, 1f), LineYColor);
            _centerSquare = CreateCenterSquare();
            _centerSquare.gameObject.SetActive(false);
        }

        // A camera-facing square at the gizmo origin. Used for free-drag vertex snapping.
        private Transform CreateCenterSquare()
        {
            const float size = 6f;
            var go = new GameObject("CenterSquare");
            go.layer = PhysicsLayer.MapEditorGizmo;
            go.transform.SetParent(_transform, false);
            go.transform.localPosition = Vector3.zero;

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            // Quad built in local XY; the transform is oriented toward the camera each frame.
            filter.sharedMesh = BuildQuadMesh(new Vector3(1f, 1f, 0f), size);

            var mat = new Material(Shader.Find("MapEditor/Gizmo"));
            mat.color = new Color(1f, 1f, 1f, 0.5f);
            mat.renderQueue = 3112;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(size, size, 0.5f);
            box.center = Vector3.zero;

            return go.transform;
        }

        private Transform CreatePlaneHandle(string name, Vector3 axisMask, Color color)
        {
            const float size = 6f;
            const float offset = 8f;
            var go = new GameObject(name);
            go.layer = PhysicsLayer.MapEditorGizmo;
            go.transform.SetParent(_transform, false);
            // Position the plane in the corner between its two axes.
            go.transform.localPosition = new Vector3(axisMask.x * offset, axisMask.y * offset, axisMask.z * offset);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = BuildQuadMesh(axisMask, size);

            var mat = new Material(Shader.Find("MapEditor/Gizmo"));
            mat.color = new Color(color.r, color.g, color.b, 0.4f);
            mat.renderQueue = 3111;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var box = go.AddComponent<BoxCollider>();
            // Size the collider to match the quad footprint.
            Vector3 sizeVec = new Vector3(
                axisMask.x > 0 ? size : 0.5f,
                axisMask.y > 0 ? size : 0.5f,
                axisMask.z > 0 ? size : 0.5f);
            box.size = sizeVec;
            box.center = Vector3.zero;

            return go.transform;
        }

        private Mesh BuildQuadMesh(Vector3 axisMask, float size)
        {
            Vector3 a, b;
            if (axisMask.x > 0 && axisMask.y > 0)
            {
                a = new Vector3(1, 0, 0); b = new Vector3(0, 1, 0);
            }
            else if (axisMask.y > 0 && axisMask.z > 0)
            {
                a = new Vector3(0, 1, 0); b = new Vector3(0, 0, 1);
            }
            else
            {
                a = new Vector3(1, 0, 0); b = new Vector3(0, 0, 1);
            }
            float half = size * 0.5f;
            Vector3 v0 = -a * half - b * half;
            Vector3 v1 =  a * half - b * half;
            Vector3 v2 =  a * half + b * half;
            Vector3 v3 = -a * half + b * half;
            // Build a double-sided quad with explicit normals so the Lambert-lit
            // MapEditor/Gizmo shader produces colored faces from either side.
            Vector3 n = Vector3.Cross(a, b).normalized;
            var verts = new Vector3[]
            {
                v0, v1, v2, v3, // front face
                v0, v1, v2, v3, // back face
            };
            var normals = new Vector3[]
            {
                n, n, n, n,
                -n, -n, -n, -n,
            };
            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = new int[]
            {
                0, 2, 1, 0, 3, 2, // front (CW from +n side)
                4, 5, 6, 4, 6, 7, // back  (CW from -n side)
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        public override void OnSelectionChange()
        {
            if (_gameManager.SelectedObjects.Count > 0 && _gameManager.CurrentGizmo == this)
            {
                gameObject.SetActive(true);
                ResetCenter();
                ResetColors();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        protected override void Update()
        {
            base.Update();
            var camera = SceneLoader.CurrentCamera;
            var mouseKey = SettingsManager.InputSettings.MapEditor.Select;
            var vertexKey = SettingsManager.InputSettings.MapEditor.VertexSnap;

            // Show/orient the center square while VertexSnap is held and nothing is being dragged.
            if (_activeLine == null && _centerSquare != null)
            {
                bool vertexMode = vertexKey.GetKey() && _gameManager.SelectedObjects.Count > 0;
                _centerSquare.gameObject.SetActive(vertexMode);
                // While vertex-snap mode is held, hide the plane (x/y/z face) handles so
                // only the axis arrows + center square remain, and move the gizmo origin
                // to the vertex on the selected mesh closest to the mouse.
                if (_planeXY != null) _planeXY.gameObject.SetActive(!vertexMode);
                if (_planeYZ != null) _planeYZ.gameObject.SetActive(!vertexMode);
                if (_planeXZ != null) _planeXZ.gameObject.SetActive(!vertexMode);
                if (vertexMode)
                {
                    if (TryFindClosestVertexOnSelection(camera, out Vector3 anchor, out Transform anchorOwner, out Vector3 anchorLocal))
                    {
                        _transform.position = anchor;
                        // Cache the current candidate anchor so it can be locked on click.
                        _snapAnchorTransform = anchorOwner;
                        _snapAnchorLocal = anchorLocal;
                        _hasSnapAnchor = true;
                    }
                    // Face the camera so the square looks square from any angle.
                    _centerSquare.rotation = Quaternion.LookRotation(_centerSquare.position - camera.Cache.Transform.position, camera.Cache.Transform.up);
                }
                else
                {
                    _hasSnapAnchor = false;
                    _snapAnchorTransform = null;
                    ResetCenter();
                }
            }

            if (_activeLine == null)
            {
                RaycastHit hit;
                if (!_menu.IsMouseUI && mouseKey.GetKeyDown() && Physics.Raycast(camera.Camera.ScreenPointToRay(Input.mousePosition), out hit, 100000f, PhysicsLayer.GetMask(PhysicsLayer.MapEditorGizmo)))
                {
                    // When V is held, the center square should take priority over any axis/plane
                    // handle that happens to overlap it in screen space.
                    if (vertexKey.GetKey() && _centerSquare != null && _centerSquare.gameObject.activeSelf)
                    {
                        var centerCollider = _centerSquare.GetComponent<Collider>();
                        Ray pickRay = camera.Camera.ScreenPointToRay(Input.mousePosition);
                        if (centerCollider != null && centerCollider.Raycast(pickRay, out RaycastHit centerHit, 100000f))
                            hit = centerHit;
                    }
                    _activeLine = hit.collider.transform;
                    _activeIsPlane = _activeLine == _planeXY || _activeLine == _planeYZ || _activeLine == _planeXZ;
                    _activeIsCenter = _activeLine == _centerSquare;
                    ResetColors();
                    SetLineColor(_activeLine, SelectedColor);
                    _previousMousePoint = hit.point;
                    if (_activeIsPlane)
                    {
                        // Capture drag plane using the handle's current normal.
                        _dragPlane = new Plane(GetPlaneNormal(_activeLine), _transform.position);
                    }
                    // If V is held when an axis arrow is picked up, capture the starting mouse
                    // position so we can infer drag direction for the surface snap.
                    _axisSnapDirection = 0;
                    _axisSnapStartMouse = Input.mousePosition;
                }
            }
            else
            {
                if (mouseKey.GetKey())
                {
                    _vertexSnapActive = vertexKey.GetKey() || _activeIsCenter;
                    if (_activeIsCenter)
                    {
                        DoFreeDragVertexSnap(camera);
                    }
                    else if (_vertexSnapActive && (_activeLine == _lineX || _activeLine == _lineY || _activeLine == _lineZ))
                    {
                        DoAxisSurfaceSnap(camera);
                    }
                    else if (_vertexSnapActive)
                    {
                        DoVertexSnap(camera);
                    }
                    else if (_activeIsPlane)
                    {
                        Ray ray = camera.Camera.ScreenPointToRay(Input.mousePosition);
                        if (_dragPlane.Raycast(ray, out float enter))
                        {
                            Vector3 mousePoint = ray.GetPoint(enter);
                            Vector3 drag = mousePoint - _previousMousePoint;
                            // Constrain the drag to the plane's two axes (already guaranteed by the plane raycast in world space, but in local mode rotate).
                            drag = ProjectDragOntoPlaneHandle(drag, _activeLine);
                            ApplySnap(ref drag, ref mousePoint);
                            MoveSelectedObjects(drag);
                            ResetCenter();
                        }
                    }
                    else
                    {
                        Ray ray = camera.Camera.ScreenPointToRay(Input.mousePosition);
                        Vector3 previousRay = (_previousMousePoint - ray.origin);
                        float angle = Vector3.Angle(ray.direction, previousRay) * Mathf.Deg2Rad;
                        float rayDistance = previousRay.magnitude * 1f / Mathf.Cos(angle);
                        Vector3 mousePoint = ray.origin + ray.direction * rayDistance;
                        Vector3 drag = mousePoint - _previousMousePoint;
                        if (_activeLine != _lineY)
                            drag += previousRay.normalized * Vector3.Dot(drag, camera.Cache.Transform.up) * 2f;
                        drag = _activeLine.right * Vector3.Dot(drag, _activeLine.right);
                        Vector3 frameDelta = Vector3.zero;

                        if (_gameManager.CurrentGizmoMode == GameManagers.GizmoMode.Center)
                        {
                            if (_activeLine == _lineX)
                                frameDelta.x = drag.x;
                            else if (_activeLine == _lineY)
                                frameDelta.y = drag.y;
                            else if (_activeLine == _lineZ)
                                frameDelta.z = drag.z;
                        }
                        else if (_gameManager.CurrentGizmoMode == GameManagers.GizmoMode.Local)
                        {
                            frameDelta = drag;
                        }


                        if (_gameManager.Snap && _gameManager.CurrentGizmoMode == GameManagers.GizmoMode.Center)
                        {
                            float snap = SettingsManager.MapEditorSettings.SnapMove.Value;
                            if (_activeLine == _lineX)
                            {
                                float x = Mathf.Round((_transform.position.x + frameDelta.x) / snap) * snap;
                                frameDelta.x = x - _transform.position.x;
                            }
                            else if (_activeLine == _lineY)
                            {
                                float y = Mathf.Round((_transform.position.y + frameDelta.y) / snap) * snap;
                                frameDelta.y = y - _transform.position.y;
                            }
                            else if (_activeLine == _lineZ)
                            {
                                float z = Mathf.Round((_transform.position.z + frameDelta.z) / snap) * snap;
                                frameDelta.z = z - _transform.position.z;
                            }
                            if (frameDelta.magnitude >= snap)
                                _previousMousePoint = mousePoint;
                        }
                        else if (_gameManager.Snap && _gameManager.CurrentGizmoMode == GameManagers.GizmoMode.Local)
                        {
                            // Snap along drag direction instead of grid.
                            float snap = SettingsManager.MapEditorSettings.SnapMove.Value;
                            float magnitude = frameDelta.magnitude;
                            float snapMagnitude = Mathf.Round(magnitude / snap) * snap;
                            frameDelta = frameDelta.normalized * snapMagnitude;
                            if (frameDelta.magnitude >= snap)
                                _previousMousePoint = mousePoint;
                        }
                        else
                            _previousMousePoint = mousePoint;
                        MoveSelectedObjects(frameDelta);
                        ResetCenter();
                    }
                    // While V-snap is active, keep the gizmo pinned to the locked anchor vertex
                    // so it doesn't snap back to the selection centroid / pivot between frames.
                    if (_vertexSnapActive && _hasSnapAnchor && _snapAnchorTransform != null)
                        _transform.position = _snapAnchorTransform.TransformPoint(_snapAnchorLocal);
                }
                else
                {
                    _gameManager.NewCommand(new TransformPositionCommand(new List<MapObject>(_gameManager.SelectedObjects)));
                    ResetColors();
                    _activeLine = null;
                    _activeIsPlane = false;
                    _activeIsCenter = false;
                    _vertexSnapActive = false;
                    _hasSnapAnchor = false;
                    _snapAnchorTransform = null;
                    _axisSnapDirection = 0;
                }
                _gameManager.IgnoreNextSelect = true;
            }
        }

        private Vector3 GetPlaneNormal(Transform plane)
        {
            if (plane == _planeXY) return _transform.forward;
            if (plane == _planeYZ) return _transform.right;
            if (plane == _planeXZ) return _transform.up;
            return Vector3.up;
        }

        // Zero out the component along the plane's normal axis so movement is constrained to the plane.
        private Vector3 ProjectDragOntoPlaneHandle(Vector3 drag, Transform plane)
        {
            if (_gameManager.CurrentGizmoMode == GameManagers.GizmoMode.Center)
            {
                if (plane == _planeXY) drag.z = 0f;
                else if (plane == _planeYZ) drag.x = 0f;
                else if (plane == _planeXZ) drag.y = 0f;
                return drag;
            }
            Vector3 n = GetPlaneNormal(plane);
            return drag - Vector3.Dot(drag, n) * n;
        }

        private void ApplySnap(ref Vector3 frameDelta, ref Vector3 mousePoint)
        {
            if (!_gameManager.Snap)
            {
                _previousMousePoint = mousePoint;
                return;
            }
            float snap = SettingsManager.MapEditorSettings.SnapMove.Value;
            if (_gameManager.CurrentGizmoMode == GameManagers.GizmoMode.Center)
            {
                Vector3 p = _transform.position + frameDelta;
                if (_activeLine == _planeXY)
                {
                    p.x = Mathf.Round(p.x / snap) * snap;
                    p.y = Mathf.Round(p.y / snap) * snap;
                }
                else if (_activeLine == _planeYZ)
                {
                    p.y = Mathf.Round(p.y / snap) * snap;
                    p.z = Mathf.Round(p.z / snap) * snap;
                }
                else if (_activeLine == _planeXZ)
                {
                    p.x = Mathf.Round(p.x / snap) * snap;
                    p.z = Mathf.Round(p.z / snap) * snap;
                }
                frameDelta = p - _transform.position;
            }
            else
            {
                float magnitude = frameDelta.magnitude;
                float snapMagnitude = Mathf.Round(magnitude / snap) * snap;
                if (magnitude > 0f)
                    frameDelta = frameDelta.normalized * snapMagnitude;
            }
            if (frameDelta.magnitude >= snap)
                _previousMousePoint = mousePoint;
        }

        // Snap the selection so that a "source" vertex of the first selected object lands on the
        // nearest vertex of any other scene object under the cursor. Mirrors Unity's V-snap.
        private void DoVertexSnap(BaseCamera camera)
        {
            if (_gameManager.SelectedObjects.Count == 0)
                return;

            // Source vertex: the locked anchor captured when V-snap was engaged, or, failing that,
            // the on-screen vertex of the selection nearest the cursor.
            Vector3 sourceVertex;
            if (_hasSnapAnchor && _snapAnchorTransform != null)
            {
                sourceVertex = _snapAnchorTransform.TransformPoint(_snapAnchorLocal);
            }
            else if (!TryFindClosestVertexOnSelection(camera, out sourceVertex))
            {
                return;
            }

            // Target vertex: the on-screen vertex of any non-selected object closest to the cursor.
            if (!TryFindNearestSceneVertexOnScreen(camera, out Vector3 targetWorldVertex))
                return;

            Vector3 delta = targetWorldVertex - sourceVertex;
            if (delta.sqrMagnitude < 1e-8f)
                return;
            foreach (MapObject obj in _gameManager.SelectedObjects)
                obj.GameObject.transform.position += delta;
        }

        // V + axis click: shoot a ray from the locked anchor vertex along the chosen axis (in the
        // direction implied by the user's drag gesture) and land the anchor on the first surface
        // hit that isn't part of the current selection. Used for "drop to ground / wall / ceiling".
        private void DoAxisSurfaceSnap(BaseCamera camera)
        {
            if (_gameManager.SelectedObjects.Count == 0 || _activeLine == null)
                return;

            Vector3 anchor;
            if (_hasSnapAnchor && _snapAnchorTransform != null)
                anchor = _snapAnchorTransform.TransformPoint(_snapAnchorLocal);
            else if (!TryFindClosestVertexOnSelection(camera, out anchor))
                return;

            // Axis world direction. Existing axis-drag code uses `_activeLine.right` as the axis
            // basis for each line, so use the same convention here.
            Vector3 axis = _activeLine.right.normalized;

            // Determine drag direction: project the mouse delta (in screen space) onto the screen
            // projection of the axis. Sign > 0 means "along +axis", < 0 means "along -axis".
            if (_axisSnapDirection == 0)
            {
                Vector2 mouseDelta = (Vector2)Input.mousePosition - _axisSnapStartMouse;
                Vector3 anchorScreen = camera.Camera.WorldToScreenPoint(anchor);
                Vector3 anchorPlusAxis = camera.Camera.WorldToScreenPoint(anchor + axis);
                Vector2 axisScreen = new Vector2(anchorPlusAxis.x - anchorScreen.x, anchorPlusAxis.y - anchorScreen.y);
                if (axisScreen.sqrMagnitude < 1e-6f || mouseDelta.sqrMagnitude < 25f)
                    return; // Not enough movement / axis is edge-on; wait.
                float dot = Vector2.Dot(mouseDelta, axisScreen.normalized);
                _axisSnapDirection = dot >= 0f ? 1 : -1;
            }

            Vector3 rayDir = axis * _axisSnapDirection;

            // Raycast against map-editor objects. Temporarily disable colliders on selected
            // objects so the ray doesn't immediately hit ourselves.
            var disabled = new List<Collider>();
            foreach (MapObject obj in _gameManager.SelectedObjects)
            {
                if (obj.GameObject == null) continue;
                foreach (var c in obj.GameObject.GetComponentsInChildren<Collider>())
                {
                    if (c.enabled)
                    {
                        c.enabled = false;
                        disabled.Add(c);
                    }
                }
            }

            bool hitFound = Physics.Raycast(anchor, rayDir, out RaycastHit hit, 100000f,
                PhysicsLayer.GetMask(PhysicsLayer.MapEditorObject));

            foreach (var c in disabled)
                c.enabled = true;

            if (!hitFound)
                return;

            Vector3 delta = hit.point - anchor;
            if (delta.sqrMagnitude < 1e-8f)
                return;
            foreach (MapObject obj in _gameManager.SelectedObjects)
                obj.GameObject.transform.position += delta;
        }

        // Free-drag vertex snap: the user is dragging the center square. The anchor vertex is the
        // one that was under (or nearest) the cursor when V-snap picked up the center square; it
        // stays locked on that specific vertex of the selected object for the whole drag. The
        // anchor snaps to the closest non-selected scene vertex within a screen-space radius;
        // if no vertex is close enough, the object moves freely along a camera-facing plane.
        private void DoFreeDragVertexSnap(BaseCamera camera)
        {
            if (_gameManager.SelectedObjects.Count == 0)
                return;

            Ray ray = camera.Camera.ScreenPointToRay(Input.mousePosition);

            // Resolve the current world position of the locked anchor vertex. Falls back to a live
            // lookup only if the anchor was never captured (shouldn't normally happen).
            Vector3 sourceVertex;
            if (_hasSnapAnchor && _snapAnchorTransform != null)
            {
                sourceVertex = _snapAnchorTransform.TransformPoint(_snapAnchorLocal);
            }
            else if (!TryFindClosestVertexOnSelection(camera, out sourceVertex))
            {
                return;
            }

            Vector3 destination;
            // World-space snap radius scales with the camera distance so the snap region stays
            // visually small regardless of how far the user has zoomed out. On huge maps this
            // prevents snapping to a plane corner that happens to line up near the cursor.
            float camDist = Vector3.Distance(camera.Cache.Transform.position, sourceVertex);
            float worldRadius = Mathf.Max(camDist * VertexSnapWorldRadiusFactor, VertexSnapWorldRadiusMin);
            if (TryFindNearestSceneVertexOnScreen(camera, VertexSnapScreenRadius, out Vector3 targetWorldVertex)
                && (targetWorldVertex - sourceVertex).sqrMagnitude <= worldRadius * worldRadius)
            {
                destination = targetWorldVertex;
            }
            else
            {
                // No nearby target vertex: project the cursor onto a camera-facing plane that
                // passes through the anchor, giving smooth free-drag behavior.
                Plane facing = new Plane(-camera.Cache.Transform.forward, sourceVertex);
                if (!facing.Raycast(ray, out float enter))
                    return;
                destination = ray.GetPoint(enter);
            }

            Vector3 delta = destination - sourceVertex;
            if (delta.sqrMagnitude < 1e-8f)
                return;
            foreach (MapObject obj in _gameManager.SelectedObjects)
                obj.GameObject.transform.position += delta;
        }

        // Find the nearest non-selected scene vertex in screen space to the current mouse cursor.
        // Only vertices whose screen projection is in front of the camera and inside the viewport
        // are considered. If `screenRadius` is finite, vertices further than that many pixels from
        // the mouse are rejected.
        private bool TryFindNearestSceneVertexOnScreen(BaseCamera camera, out Vector3 vertex)
        {
            return TryFindNearestSceneVertexOnScreen(camera, float.PositiveInfinity, out vertex);
        }

        private bool TryFindNearestSceneVertexOnScreen(BaseCamera camera, float screenRadius, out Vector3 vertex)
        {
            vertex = Vector3.zero;
            Vector2 mouseScreen = Input.mousePosition;
            float bestScreen = float.IsPositiveInfinity(screenRadius) ? float.MaxValue : screenRadius * screenRadius;
            float bestDepth = float.MaxValue;
            float tiebreakSqr = VertexSnapDepthTiebreakPixels * VertexSnapDepthTiebreakPixels;
            bool found = false;
            float screenW = Screen.width, screenH = Screen.height;
            var selected = _gameManager.SelectedObjects;
            foreach (var pair in MapLoader.IdToMapObject)
            {
                var mapObject = pair.Value;
                if (selected.Contains(mapObject))
                    continue;
                var go = mapObject.GameObject;
                if (go == null || !go.activeInHierarchy)
                    continue;
                foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null)
                        continue;
                    var mr = filter.GetComponent<MeshRenderer>();
                    if (mr != null && !ScreenBoundsIntersectsMouse(mr.bounds, camera.Camera, mouseScreen, float.IsPositiveInfinity(screenRadius) ? Mathf.Max(screenW, screenH) : screenRadius))
                        continue;
                    var verts = filter.sharedMesh.vertices;
                    var t = filter.transform;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 world = t.TransformPoint(verts[i]);
                        Vector3 screen = camera.Camera.WorldToScreenPoint(world);
                        if (screen.z <= 0f) continue;
                        if (screen.x < 0f || screen.y < 0f || screen.x > screenW || screen.y > screenH) continue;
                        float dx = screen.x - mouseScreen.x;
                        float dy = screen.y - mouseScreen.y;
                        float distSqr = dx * dx + dy * dy;
                        bool better = false;
                        if (distSqr + tiebreakSqr < bestScreen)
                        {
                            better = true;
                        }
                        else if (distSqr <= bestScreen + tiebreakSqr && Mathf.Abs(distSqr - bestScreen) <= tiebreakSqr && screen.z < bestDepth)
                        {
                            better = true;
                        }
                        if (better)
                        {
                            bestScreen = distSqr;
                            bestDepth = screen.z;
                            vertex = world;
                            found = true;
                        }
                    }
                }
            }
            return found;
        }

        // Project a world-space AABB into screen space and test if its rect is within
        // `screenRadius` pixels of `mouseScreen`. Returns true if any corner is in front of the
        // camera AND the projected rect intersects the mouse's inflated radius.
        private static bool ScreenBoundsIntersectsMouse(Bounds bounds, Camera cam, Vector2 mouseScreen, float screenRadius)
        {
            Vector3 min = bounds.min, max = bounds.max;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool anyInFront = false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 s = cam.WorldToScreenPoint(c);
                if (s.z > 0f)
                    anyInFront = true;
                if (s.x < minX) minX = s.x;
                if (s.y < minY) minY = s.y;
                if (s.x > maxX) maxX = s.x;
                if (s.y > maxY) maxY = s.y;
            }
            if (!anyInFront)
                return false;
            return mouseScreen.x >= minX - screenRadius && mouseScreen.x <= maxX + screenRadius
                && mouseScreen.y >= minY - screenRadius && mouseScreen.y <= maxY + screenRadius;
        }

        // Find the vertex across all selected objects whose screen projection is closest to the
        // mouse cursor (in pixels). Only vertices in front of the camera and inside the viewport
        // are considered. Also returns the owning transform and its local-space position so the
        // caller can re-evaluate the world position after the object moves.
        private bool TryFindClosestVertexOnSelection(BaseCamera camera, out Vector3 vertex, out Transform owner, out Vector3 localVertex)
        {
            vertex = Vector3.zero;
            owner = null;
            localVertex = Vector3.zero;
            Vector2 mouseScreen = Input.mousePosition;
            float bestScreen = float.MaxValue;
            float bestDepth = float.MaxValue;
            float tiebreakSqr = VertexSnapDepthTiebreakPixels * VertexSnapDepthTiebreakPixels;
            bool found = false;
            float screenW = Screen.width, screenH = Screen.height;
            foreach (MapObject obj in _gameManager.SelectedObjects)
            {
                var go = obj.GameObject;
                if (go == null)
                    continue;
                foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null)
                        continue;
                    var mr = filter.GetComponent<MeshRenderer>();
                    if (mr != null && !ScreenBoundsIntersectsMouse(mr.bounds, camera.Camera, mouseScreen, Mathf.Max(screenW, screenH)))
                        continue;
                    var verts = filter.sharedMesh.vertices;
                    var t = filter.transform;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 world = t.TransformPoint(verts[i]);
                        Vector3 screen = camera.Camera.WorldToScreenPoint(world);
                        if (screen.z <= 0f) continue;
                        if (screen.x < 0f || screen.y < 0f || screen.x > screenW || screen.y > screenH) continue;
                        float dx = screen.x - mouseScreen.x;
                        float dy = screen.y - mouseScreen.y;
                        float distSqr = dx * dx + dy * dy;
                        // Primary: closest in screen space. Tiebreak (within a few pixels):
                        // pick the vertex nearest the camera so front-face vertices win over
                        // back-face vertices that project to the same pixel.
                        bool better = false;
                        if (distSqr + tiebreakSqr < bestScreen)
                        {
                            better = true;
                        }
                        else if (Mathf.Abs(distSqr - bestScreen) <= tiebreakSqr && screen.z < bestDepth)
                        {
                            better = true;
                        }
                        if (better)
                        {
                            bestScreen = distSqr;
                            bestDepth = screen.z;
                            vertex = world;
                            owner = t;
                            localVertex = verts[i];
                            found = true;
                        }
                    }
                }
            }
            return found;
        }

        private bool TryFindClosestVertexOnSelection(BaseCamera camera, out Vector3 vertex)
        {
            return TryFindClosestVertexOnSelection(camera, out vertex, out _, out _);
        }

        private void MoveSelectedObjects(Vector3 frameDelta)
        {
            foreach (MapObject obj in _gameManager.SelectedObjects)
                obj.GameObject.transform.position += frameDelta;
        }

        private void ResetCenter()
        {
            Vector3 totalPosition = new Vector3();
            _transform.rotation = Quaternion.identity;
            foreach (MapObject obj in _gameManager.SelectedObjects)
            {
                totalPosition += obj.GameObject.transform.position;
                if (_gameManager.CurrentGizmoMode == GameManagers.GizmoMode.Local)
                    _transform.rotation = obj.GameObject.transform.rotation;
            }
            Vector3 center = totalPosition / _gameManager.SelectedObjects.Count;
            _transform.position = center;
        }

        private void ResetColors()
        {
            SetLineColor(_lineX, LineXColor);
            SetLineColor(_lineY, LineYColor);
            SetLineColor(_lineZ, LineZColor);
            if (_planeXY != null) SetLineColor(_planeXY, new Color(LineZColor.r, LineZColor.g, LineZColor.b, 0.4f));
            if (_planeYZ != null) SetLineColor(_planeYZ, new Color(LineXColor.r, LineXColor.g, LineXColor.b, 0.4f));
            if (_planeXZ != null) SetLineColor(_planeXZ, new Color(LineYColor.r, LineYColor.g, LineYColor.b, 0.4f));
        }

        private void SetLineColor(Transform line, Color color)
        {
            foreach (Renderer renderer in line.GetComponentsInChildren<Renderer>())
            {
                renderer.material.color = color;
                renderer.material.renderQueue = 3111;
            }
        }
    }
}
