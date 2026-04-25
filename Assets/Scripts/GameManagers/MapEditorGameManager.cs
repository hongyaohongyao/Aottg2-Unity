using System.Collections.Generic;
using UnityEngine;
using Weather;
using UI;
using Utility;
using CustomSkins;
using ApplicationManagers;
using Map;
using Settings;
using MapEditor;
using CustomLogic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using static UI.MapEditorTopPanel;
using System;

namespace GameManagers
{
    enum GizmoMode
    {
        Center,
        Local
    }

    class MapEditorGameManager : BaseGameManager
    {
        public MapScript MapScript;
        public CustomLogicEvaluator LogicEvaluator;
        public HashSet<MapObject> SelectedObjects = new HashSet<MapObject>();
        public BaseGizmo CurrentGizmo;
        public GizmoMode CurrentGizmoMode;
        public bool Snap;
        private List<BaseCommand> _undoCommands = new List<BaseCommand>();
        private List<BaseCommand> _redoCommands = new List<BaseCommand>();
        private string _clipboard = string.Empty;
        private MapEditorMenu _menu;
        private MapEditorInputSettings _input;
        public PositionGizmo _positionGizmo;
        public RotationGizmo _rotationGizmo;
        public ScaleGizmo _scaleGizmo;
        private OutlineGizmo _outlineGizmo;
        public MapEditorBrush Brush = new MapEditorBrush();
        private int _currentObjectId;
        public bool IgnoreNextSelect;
        private bool _isDrag;
        private Vector3 _dragStart;
        
        public void ShowAddObject()
        {
            if (_menu.AddObjectPopup.IsActive)
                _menu.AddObjectPopup.Hide();
            else if (!_menu.IsMouseUI)
                _menu.AddObjectPopup.Show();
        }

        public void AddObject(string name)
        {
            var mapScriptObjects = new MapScriptObjects();
            MapScriptSceneObject prefab;
            if (name.StartsWith("Custom/"))
            {
                prefab = new MapScriptSceneObject();
                prefab.Asset = name;
                prefab.Name = name.Split('/')[2];
                prefab.Material.Shader = MapObjectShader.DefaultNoTint;
            }
            else
                prefab = (MapScriptSceneObject)BuiltinMapPrefabs.AllPrefabs[name];
            var position = SceneLoader.CurrentCamera.Cache.Transform.position + SceneLoader.CurrentCamera.Cache.Transform.forward * SettingsManager.MapEditorSettings.PlacementDistance.Value;

            // Shoot a ray towards position and if something hits, change the position to the hit
            if (SettingsManager.MapEditorSettings.PlaceOnFirstSurface.Value)
            {
                RaycastHit hit;
                Ray ray = new Ray(SceneLoader.CurrentCamera.Cache.Transform.position, position - SceneLoader.CurrentCamera.Cache.Transform.position);
                if (Physics.Raycast(ray, out hit, SettingsManager.MapEditorSettings.PlacementDistance.Value, PhysicsLayer.GetMask(PhysicsLayer.MapEditorObject)))
                    position = hit.point;
            }
            

            // if snap is enabled, round the position to the nearest snap distance
            if (((MapEditorGameManager)SceneLoader.CurrentGameManager).Snap)
            {
                float snap = SettingsManager.MapEditorSettings.SnapMove.Value;
                float x = Mathf.Round(position.x / snap) * snap;
                float y = Mathf.Round(position.y / snap) * snap;
                float z = Mathf.Round(position.z / snap) * snap;
                position = new Vector3(x, y, z);
            }
            prefab.SetPosition(position);
            mapScriptObjects.Objects.Add(prefab);
            NewCommand(new AddObjectCommand(mapScriptObjects.Objects));
            DeselectAll();
            SelectObject(MapLoader.IdToMapObject[_currentObjectId]);
            _menu.SyncHierarchyPanel();
            OnSelectionChange();
        }

        public void Undo()
        {
            if (_undoCommands.Count == 0)
                return;
            var command = _undoCommands[_undoCommands.Count - 1];
            command.Unexecute();
            _redoCommands.Add(command);
            _undoCommands.RemoveAt(_undoCommands.Count - 1);
            if (command is AddObjectCommand || command is DeleteObjectCommand)
                _menu.SyncHierarchyPanel();
            OnSelectionChange();
        }

        public void Redo()
        {
            if (_redoCommands.Count == 0)
                return;
            var command = _redoCommands[_redoCommands.Count - 1];
            command.Execute();
            _undoCommands.Add(command);
            _redoCommands.RemoveAt(_redoCommands.Count - 1);
            if (command is AddObjectCommand || command is DeleteObjectCommand)
                _menu.SyncHierarchyPanel();
            OnSelectionChange();
        }

        public void Copy()
        {
            if (SelectedObjects.Count == 0)
                return;
            var mapScriptObjects = new MapScriptObjects();
            foreach (var obj in SelectedObjects)
                mapScriptObjects.Objects.Add(obj.ScriptObject);
            _clipboard = mapScriptObjects.Serialize();
        }

        public void Paste()
        {
            if (_clipboard == string.Empty)
                return;
            var mapScriptObjects = new MapScriptObjects();
            mapScriptObjects.Deserialize(_clipboard);
            NewCommand(new AddObjectCommand(mapScriptObjects.Objects));
            DeselectAll();
            foreach (var obj in mapScriptObjects.Objects)
                SelectObject(MapLoader.IdToMapObject[obj.Id]);
            _menu.SyncHierarchyPanel();
            OnSelectionChange();
        }

        public void Cut()
        {
            Copy();
            Delete();
        }

        public void Delete()
        {
            if (SelectedObjects.Count == 0)
                return;
            NewCommand(new DeleteObjectCommand(new List<MapObject>(SelectedObjects)));
            _menu.SyncHierarchyPanel();
            OnSelectionChange();
        }

        public void Select(bool multi)
        {
            // Selection is disabled while the brush tool is active so that clicking
            // to paint doesn't also pick whatever object is under the cursor.
            if (Brush != null && Brush.Active)
                return;
            var camera = SceneLoader.CurrentCamera;
            Vector3 diff = Input.mousePosition - _dragStart;
            RaycastHit hit;
            if (diff.magnitude < 1f)
            {
                if (!_menu.IsMouseUI)
                {
                    if (Physics.Raycast(camera.Camera.ScreenPointToRay(Input.mousePosition), out hit, 100000f, PhysicsLayer.GetMask(PhysicsLayer.MapEditorObject)))
                    {

                        var mapObject = MapLoader.FindObjectFromCollider(hit.collider);
                        if (multi)
                        {
                            if (SelectedObjects.Contains(mapObject))
                                DeselectObject(mapObject);
                            else
                                SelectObject(mapObject);
                        }
                        else
                        {
                            if (SelectedObjects.Count == 1 && SelectedObjects.Contains(mapObject))
                                DeselectAll();
                            else if (SelectedObjects.Count > 0)
                            {
                                DeselectAll();
                                SelectObject(mapObject);
                            }
                            else
                                SelectObject(mapObject);
                        }
                    }
                    else if (!multi)
                        DeselectAll();
                }
            }
            else
            {
                if (!multi)
                    DeselectAll();
                foreach (var gameObject in MapLoader.GoToMapObject.Keys)
                {
                    var mapObject = MapLoader.GoToMapObject[gameObject];
                    foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>())
                    {
                        if (renderer != null && renderer.gameObject.activeSelf)
                        {
                            Vector3 center = renderer.bounds.center;
                            Vector2 screenPosition = camera.Camera.WorldToScreenPoint(center);
                            if (Vector3.Distance(center, camera.Cache.Transform.position) < camera.Camera.farClipPlane &&
                                Util.IsVectorBetween(screenPosition, (Vector2)_dragStart, (Vector2)Input.mousePosition) &&
                                renderer.isVisible)
                            {
                                if (!SelectedObjects.Contains(mapObject))
                                    SelectObject(mapObject);
                                break;
                            }
                        }
                    }
                }
            }
            OnSelectionChange();
        }

        public void DeselectAll()
        {
            foreach (MapObject obj in new List<MapObject>(SelectedObjects))
                DeselectObject(obj);
        }

        public void DeselectObject(MapObject obj)
        {
            SelectedObjects.Remove(obj);
        }

        public void SelectObject(MapObject obj)
        {
            SelectedObjects.Add(obj);
        }

        public void NewCommand(BaseCommand command, bool syncInspector = true)
        {
            command.Execute();
            _undoCommands.Add(command);
            _redoCommands.Clear();
            if (command is TransformPositionCommand || command is TransformPositionRotationCommand || command is TransformScaleCommand)
            {
                if (syncInspector)
                    _menu.SyncInspector();
            }
        }

        protected override void OnFinishLoading()
        {
            MapScript = MapManager.MapScript;
            _menu = (MapEditorMenu)UIManager.CurrentMenu;
            _positionGizmo = PositionGizmo.Create();
            _outlineGizmo = OutlineGizmo.Create();
            _rotationGizmo = RotationGizmo.Create();
            _scaleGizmo = ScaleGizmo.Create();
            _currentObjectId = GetHighestObjectId();
            _menu.ShowHierarchyPanel();
            LogicEvaluator = CustomLogicManager.GetEditorEvaluator(MapScript.Logic);
            CurrentGizmo = _positionGizmo;
            if (MapLoader.Errors.Count > 0)
                _menu.ErrorPopup.Show(string.Join("\n", MapLoader.Errors));
            StartCoroutine(WaitAndAutosave());
        }

        protected override void Awake()
        {
            base.Awake();
            _input = SettingsManager.InputSettings.MapEditor;
        }

        private IEnumerator WaitAndAutosave()
        {
            while (true)
            {
                yield return new WaitForSeconds(900f);
                _menu._topPanel.Autosave();
            }
        }

        protected void Update()
        {
            UpdateInput();
            UpdateBrush();
            UpdateDrag();
        }

        protected void UpdateBrush()
        {
            if (Brush == null || !Brush.Active) return;
            if (_menu == null) return;
            if (_menu.IsPopupActive() || _menu.IsMouseUI)
            {
                Brush.ClearCenter();
                return;
            }
            var system = EventSystem.current;
            var selected = system != null ? system.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponent<InputField>() != null)
            {
                Brush.ClearCenter();
                return;
            }
            var camera = SceneLoader.CurrentCamera;
            if (camera == null) return;
            Ray ray = camera.Camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100000f, PhysicsLayer.GetMask(PhysicsLayer.MapEditorObject)))
                Brush.SetCenter(hit.point);
            else
                Brush.ClearCenter();

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            bool shift = _input.BrushCount.GetKey();
            bool ctrl = _input.BrushCurve.GetKey();
            if (Brush.Mode == BrushMode.Grid)
            {
                if (_input.BrushCurveAxisX.GetKeyDown()) Brush.CurveAxis = 1;
                else if (_input.BrushCurveAxisY.GetKeyDown()) Brush.CurveAxis = 2;
                else if (_input.BrushCurveAxisZ.GetKeyDown()) Brush.CurveAxis = 3;
                else if (_input.BrushCurveAxisNone.GetKeyDown()) Brush.CurveAxis = 0;
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    // Scroll alone: spacing along the growth axis (falls back to X).
                    // Shift+scroll: add/remove objects along the growth axis.
                    // Ctrl+scroll: curve amount along the growth axis.
                    int axis = Brush.CurveAxis == 0 ? 1 : Brush.CurveAxis;
                    int sign = scroll > 0f ? 1 : -1;
                    if (ctrl)
                    {
                        Brush.CurveAmount += sign * 0.5f;
                    }
                    else if (shift)
                    {
                        if (axis == 1) Brush.GridX = Mathf.Max(1, Brush.GridX + sign);
                        else if (axis == 2) Brush.GridY = Mathf.Max(1, Brush.GridY + sign);
                        else if (axis == 3) Brush.GridZ = Mathf.Max(1, Brush.GridZ + sign);
                    }
                    else
                    {
                        // Scale spacing step to roughly 10% of the asset's bounding box along the growth axis
                        // so scrolling through a few ticks makes a visible difference regardless of asset size.
                        Vector3 avg = Brush.GetAverageGhostSize();
                        float axisSize = axis == 1 ? avg.x : axis == 2 ? avg.y : avg.z;
                        float step = Mathf.Max(0.5f, axisSize * 0.1f);
                        if (axis == 1) Brush.SpacingX = Mathf.Max(0f, Brush.SpacingX + sign * step);
                        else if (axis == 2) Brush.SpacingY = Mathf.Max(0f, Brush.SpacingY + sign * step);
                        else if (axis == 3) Brush.SpacingZ = Mathf.Max(0f, Brush.SpacingZ + sign * step);
                    }
                    Brush.RegenerateGhosts();
                    _menu.SyncBrushPanelValues();
                }
            }
            else if (Mathf.Abs(scroll) > 0.0001f)
                Brush.SetRadius(Brush.Radius * (1f + scroll * 2f));

            if (_input.BrushReroll.GetKeyDown())
            {
                Brush.Reseed();
                Brush.RegenerateGhosts();
            }

            if (_input.Select.GetKeyDown())
                PlaceBrush();
        }

        private void PlaceBrush()
        {
            if (!Brush.Active) return;
            var placements = Brush.BuildPlacements();
            if (placements.Count == 0) return;
            NewCommand(new AddObjectCommand(placements));
            _menu.SyncHierarchyPanel();
            if (Brush.RandomizeEachPlacement)
                Brush.Reseed();
            Brush.RegenerateGhosts();
        }

        public void ToggleBrush()
        {
            bool active = !Brush.Active;
            Brush.SetActive(active);
            if (active)
            {
                DeselectAll();
                _menu.ShowBrushPanel();
            }
            else
            {
                _menu.HideBrushPanel();
            }
            OnSelectionChange();
        }

        protected void UpdateDrag()
        {
            if (_menu == null)
                return;
            if (CurrentGizmo != null && CurrentGizmo.IsActive())
                _isDrag = false;
            if (_menu.IsPopupActive())
                _isDrag = false;
            var system = EventSystem.current;
            var selected = system.currentSelectedGameObject;
            if (selected != null && selected.GetComponent<InputField>() != null)
                _isDrag = false;
            if (_isDrag)
            {
                _menu.SetDrag(true, _dragStart, Input.mousePosition);
                if (_input.Select.GetKeyUp())
                {
                    Select(_input.Multiselect.GetKey() && SelectedObjects.Count > 0);
                    _isDrag = false;
                    _menu.SetDrag(false, Vector2.zero, Vector2.zero);
                }
            }
            else
            {
                _menu.SetDrag(false, Vector2.zero, Vector2.zero);
            }
        }

        protected void UpdateInput()
        {
            if (_menu == null)
                return;
            if (_input.SaveMap.GetKeyDown())
            {
                Save();
                return;
            }
            if (_menu.IsPopupActive())
                return;
            var system = EventSystem.current;
            var selected = system.currentSelectedGameObject;
            if (selected != null && selected.GetComponent<InputField>() != null)
                return;
            if (_input.Undo.GetKeyDown())
                Undo();
            else if (_input.Redo.GetKeyDown())
                Redo();
            else if (_input.CopyObjects.GetKeyDown())
                Copy();
            else if (_input.Paste.GetKeyDown())
                Paste();
            else if (_input.Cut.GetKeyDown())
                Cut();
            else if (_input.AddObject.GetKeyDown())
                ShowAddObject();
            else if (_input.Delete.GetKeyDown())
                Delete();
            else if (_input.Deselect.GetKeyDown())
            {
                DeselectAll();
                OnSelectionChange();
            }
            else if (_input.ChangeGizmo.GetKeyDown())
                _menu._topPanel.NextGizmo();
            else if (_input.ToggleSnap.GetKeyDown())
                _menu._topPanel.ToggleSnap();
            else if (_input.Select.GetKeyDown() && !_menu.IsMouseUI)
            {
                _isDrag = true;
                _dragStart = Input.mousePosition;
            }
            IgnoreNextSelect = false;
        }

        public void Save()
        {
            _menu._topPanel.Save();
        }

        public void OnSelectionChange()
        {
            foreach (var obj in new HashSet<MapObject>(SelectedObjects))
            {
                if (!MapLoader.IdToMapObject.ContainsKey(obj.ScriptObject.Id))
                    DeselectObject(obj);
            }
            if (SelectedObjects.Count >= 1 && !Brush.Active)
                _menu.ShowInspector(new List<MapObject>(SelectedObjects));
            else
                _menu.HideInspector();
            _menu.HierarchyPanel.SyncSelectedItems();
            SyncGizmos();
        }

        public void SyncGizmos()
        {
            _outlineGizmo.OnSelectionChange();
            _positionGizmo.OnSelectionChange();
            _rotationGizmo.OnSelectionChange();
            _scaleGizmo.OnSelectionChange();
        }

        public void SetGizmo(string gizmo)
        {
            if (gizmo == "Position")
                CurrentGizmo = _positionGizmo;
            else if (gizmo == "Rotation")
                CurrentGizmo = _rotationGizmo;
            else
                CurrentGizmo = _scaleGizmo;
            SyncGizmos();
        }

        public void SetGizmoMode(GizmoMode mode)
        {
            CurrentGizmoMode = mode;
            SyncGizmos();
        }

        public int GetNextObjectId()
        {
            _currentObjectId++;
            return _currentObjectId;
        }

        protected int GetHighestObjectId()
        {
            int max = 0;
            foreach (int id in MapLoader.IdToMapObject.Keys)
            {
                max = Mathf.Max(max, id);
            }
            return max;
        }
        
        private bool _lightsOn = false;

        private T TryGetSetting<T>(Dictionary<string, BaseSetting> settings, string key, T Default) where T : BaseSetting
        {
            if (settings.ContainsKey(key))
            {
                if (settings[key] is T)
                    return (T)settings[key];
            }
            return Default;
        }

        public void ToggleLights()
        {
            if (_lightsOn)
            {
                foreach (var obj in MapLoader.IdToMapObject.Values)
                {
                    if (obj.GameObject.GetComponent<Light>() != null)
                        obj.GameObject.GetComponent<Light>().enabled = false;
                }
                _lightsOn = false;
                return;
            }
            _lightsOn = true;
            var manager = (MapEditorGameManager)SceneLoader.CurrentGameManager;
            foreach (var obj in MapLoader.IdToMapObject.Values)
            {
                if (obj.GameObject.GetComponent<Light>() != null)
                {
                    obj.GameObject.GetComponent<Light>().enabled = true;
                    continue;
                }
                if (obj.ScriptObject is MapScriptSceneObject)
                {
                    var sceneObject = (MapScriptSceneObject)obj.ScriptObject;
                    var components = sceneObject.Components;
                    foreach (var component in components)
                    {
                        var settings = manager.LogicEvaluator.GetComponentSettings(component.ComponentName, component.Parameters);
                        if (component.ComponentName == "Daylight")
                        {
                            var colorSetting = TryGetSetting<ColorSetting>(settings, "Color", null);
                            var intensitySetting = TryGetSetting<FloatSetting>(settings, "Intensity", null);
                            if (colorSetting != null && intensitySetting != null)
                            {
                                var light = obj.GameObject.AddComponent<Light>();
                                light.type = LightType.Directional;
                                light.color = colorSetting.Value.ToColor();
                                light.intensity = intensitySetting.Value;
                                light.shadows = LightShadows.Hard;
                                break;
                            }
                        }
                        else if (component.ComponentName == "PointLight")
                        {
                            var colorSetting = TryGetSetting<ColorSetting>(settings, "Color", null);
                            var intensitySetting = TryGetSetting<FloatSetting>(settings, "Intensity", null);
                            var rangeSetting = TryGetSetting<FloatSetting>(settings, "Range", null);
                            if (colorSetting != null && intensitySetting != null && rangeSetting != null)
                            {
                                var light = obj.GameObject.AddComponent<Light>();
                                light.type = LightType.Point;
                                light.color = colorSetting.Value.ToColor();
                                light.intensity = intensitySetting.Value;
                                light.range = rangeSetting.Value;
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void SetLayerVisibility(HashSetSetting<int> selectedLayers)
        {
            foreach (var obj in MapLoader.IdToMapObject.Values)
            {
                if (!(obj.ScriptObject is MapScriptSceneObject))
                    continue;

                var sceneObject = (MapScriptSceneObject)obj.ScriptObject;
                bool matchesFilter = false;

                // If "All" is selected, show everything
                if (selectedLayers.Contains((int)LayerOption.All))
                {
                    matchesFilter = true;
                }
                else
                {
                    // Check each selected filter - object matches if it passes ANY selected filter
                    foreach (int layerIndex in selectedLayers.Value)
                    {
                        LayerOption option = (LayerOption)layerIndex;
                        bool matches = option switch
                        {
                            LayerOption.Visible => sceneObject.Visible,
                            LayerOption.Invisible => !sceneObject.Visible,
                            LayerOption.Active => sceneObject.Active,
                            LayerOption.Inactive => !sceneObject.Active,
                            LayerOption.Static => sceneObject.Static,
                            LayerOption.NonStatic => !sceneObject.Static,
                            LayerOption.Networked => sceneObject.Networked,
                            LayerOption.NonNetworked => !sceneObject.Networked,
                            LayerOption.Triggers => sceneObject.CollideMode == "Region",
                            LayerOption.Colliders => sceneObject.CollideMode == "Physical",
                            LayerOption.NoColliders => sceneObject.CollideMode == "None",
                            _ => false,
                        };
                        if (matches)
                        {
                            matchesFilter = true;
                            break;
                        }
                    }
                }

                obj.GameObject.SetActive(matchesFilter);
            }
        }
    }
}
