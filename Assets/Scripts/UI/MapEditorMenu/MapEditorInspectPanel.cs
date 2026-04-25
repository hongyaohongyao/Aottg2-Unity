using ApplicationManagers;
using CustomLogic;
using GameManagers;
using Map;
using MapEditor;
using Settings;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace UI
{
    class MapEditorInspectPanel : HeadedPanel
    {
        protected override float Width => 400f;
        protected override float Height => 990f;
        protected override float TopBarHeight => 0f;
        protected override float BottomBarHeight => 0f;
        protected override float VerticalSpacing => 10f;
        protected override int HorizontalPadding => 25;
        protected override int VerticalPadding => 15;
        protected override bool ScrollBar => true;
        private MapEditorGameManager _gameManager;
        private MapEditorMenu _menu;
        public MapObject _mapObject;
        public List<MapObject> _mapObjects = new List<MapObject>();
        private HashSet<string> _mixedFields = new HashSet<string>();
        private Dictionary<string, string> _syncedSerialized = new Dictionary<string, string>();
        private List<GameObject> _mixedNumericElements = new List<GameObject>();
        private const string MixedStringValue = "--";
        private IntSetting _parent = new IntSetting();
        private StringSetting _name = new StringSetting();
        private BoolSetting _active = new BoolSetting();
        private BoolSetting _static = new BoolSetting();
        private BoolSetting _networked = new BoolSetting();
        private BoolSetting _visible = new BoolSetting();
        private FloatSetting _positionX = new FloatSetting();
        private FloatSetting _positionY = new FloatSetting();
        private FloatSetting _positionZ = new FloatSetting();
        private FloatSetting _rotationX = new FloatSetting();
        private FloatSetting _rotationY = new FloatSetting();
        private FloatSetting _rotationZ = new FloatSetting();
        private FloatSetting _scaleX = new FloatSetting();
        private FloatSetting _scaleY = new FloatSetting();
        private FloatSetting _scaleZ = new FloatSetting();
        private StringSetting _collideMode = new StringSetting();
        private StringSetting _collideWith = new StringSetting();
        private StringSetting _physicsMaterial = new StringSetting();
        private StringSetting _shader = new StringSetting();
        private ColorSetting _color = new ColorSetting();
        private ColorSetting _reflectColor = new ColorSetting();
        private StringSetting _texture = new StringSetting("Misc/None");
        private FloatSetting _tilingX = new FloatSetting(1f);
        private FloatSetting _tilingY = new FloatSetting(1f);
        private FloatSetting _offsetX = new FloatSetting();
        private FloatSetting _offsetY = new FloatSetting();
        private List<Dictionary<string, BaseSetting>> _components = new List<Dictionary<string, BaseSetting>>();
        private List<string> _componentNames = new List<string>();

        public override void Setup(BasePanel parent = null)
        {
            base.Setup(parent);
            _menu = (MapEditorMenu)UIManager.CurrentMenu;
            _gameManager = (MapEditorGameManager)SceneLoader.CurrentGameManager;
        }

        private bool HasNonConvexMeshCollider(MapObject mapObject)
        {
            foreach (Collider c in mapObject.GameObject.GetComponentsInChildren<Collider>())
            {
                if (c is MeshCollider && !((MeshCollider)c).convex)
                    return true;
            }
            return false;
        }

        public void CopyID()
        {
            GUIUtility.systemCopyBuffer = this._mapObject.ScriptObject.Id.ToString();
        }

        public void CopyAssetCSV()
        {
            string serialized = this._mapObject.ScriptObject.Serialize();
            GUIUtility.systemCopyBuffer = serialized;
        }

        public void Show(MapObject mapObject)
        {
            Show(new List<MapObject> { mapObject });
        }

        public void Show(List<MapObject> mapObjects)
        {
            base.Show();
            _mapObjects = new List<MapObject>(mapObjects);
            _mapObject = _mapObjects[0];
            _mixedFields.Clear();
            _mixedNumericElements.Clear();
            SyncSettings();
            ElementStyle style = new ElementStyle(fontSize: 18, titleWidth: 80f, spacing: 10f, themePanel: ThemePanel);
            if (_mapObjects.Count == 1)
            {
                var topGroup = ElementFactory.CreateHorizontalGroup(SinglePanel, 20f, TextAnchor.MiddleLeft);
                ElementFactory.CreateIconButton(topGroup.transform, style, "Icons/Navigation/CopyIcon", onClick: () => CopyAssetCSV());

                var topGroupVert = ElementFactory.CreateVerticalGroup(topGroup.transform, 10f, TextAnchor.UpperLeft);
                var idLabel = ElementFactory.CreateTextButton(topGroupVert.transform, style, "Id: " + _mapObject.ScriptObject.Id.ToString(), onClick: () => CopyID());
                var assetLabel = ElementFactory.CreateTextButton(topGroupVert.transform, style, "Asset: " + _mapObject.ScriptObject.Asset, onClick: () => CopyAssetCSV());  // This will eventually allow swapping asset.
            }
            else
            {
                ElementFactory.CreateDefaultLabel(SinglePanel, style, "Selected " + _mapObjects.Count.ToString() + " objects", alignment: TextAnchor.MiddleLeft);
            }

            // var label = ElementFactory.CreateDefaultLabel(topGroup.transform, style, "Id: " + _mapObject.ScriptObject.Id.ToString() + "\n" + "Asset: " + _mapObject.ScriptObject.Asset, alignment: TextAnchor.MiddleLeft);
            // label.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;    // Doesn't stop the object from consuming icon space.
            var group = ElementFactory.CreateHorizontalGroup(SinglePanel, 20f, TextAnchor.MiddleLeft).transform;
            style.TitleWidth = 45f;
            ElementFactory.CreateToggleSetting(group, style, _active, "Active", elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnChange());
            ElementFactory.CreateToggleSetting(group, style, _static, "Static", elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnChange());
            style.TitleWidth = 80f;
            ElementFactory.CreateToggleSetting(group, style, _networked, "Networked", elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnChange());
            ElementFactory.CreateInputSetting(SinglePanel, style, _name, "Name", elementWidth: 140f, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateInputSetting(SinglePanel, style, _parent, "Parent Id", elementWidth: 140f, elementHeight: 35f, onEndEdit: () => OnChange());
            CreateHorizontalDivider(SinglePanel);
            float inputWidth = 80f;
            style = new ElementStyle(fontSize: 18, titleWidth: 15f, spacing: 5f, themePanel: ThemePanel);
            ElementFactory.CreateDefaultLabel(SinglePanel, style, "Position");
            group = ElementFactory.CreateHorizontalGroup(SinglePanel, 15f, TextAnchor.MiddleLeft).transform;
            ElementFactory.CreateInputSetting(group, style, _positionX, "X", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateInputSetting(group, style, _positionY, "Y", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateInputSetting(group, style, _positionZ, "Z", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateDefaultLabel(SinglePanel, style, "Rotation");
            group = ElementFactory.CreateHorizontalGroup(SinglePanel, 15f, TextAnchor.MiddleLeft).transform;
            ElementFactory.CreateInputSetting(group, style, _rotationX, "X", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateInputSetting(group, style, _rotationY, "Y", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateInputSetting(group, style, _rotationZ, "Z", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateDefaultLabel(SinglePanel, style, "Scale");
            group = ElementFactory.CreateHorizontalGroup(SinglePanel, 15f, TextAnchor.MiddleLeft).transform;
            ElementFactory.CreateInputSetting(group, style, _scaleX, "X", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateInputSetting(group, style, _scaleY, "Y", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            ElementFactory.CreateInputSetting(group, style, _scaleZ, "Z", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            CreateHorizontalDivider(SinglePanel);
            style = new ElementStyle(fontSize: 18, titleWidth: 160f, spacing: 20f, themePanel: ThemePanel);
            string[] collideModeOptions = !_mapObjects.Any(HasNonConvexMeshCollider)
                ? new string[] { MapObjectCollideMode.Physical, MapObjectCollideMode.Region, MapObjectCollideMode.None }
                : new string[] { MapObjectCollideMode.Physical, MapObjectCollideMode.None };
            ElementFactory.CreateDropdownSetting(SinglePanel, style, _collideMode, "Collide Mode",
                WithMixedOption(collideModeOptions, "CollideMode", _collideMode),
                elementHeight: 30f, onDropdownOptionSelect: () => OnChange());
            ElementFactory.CreateDropdownSetting(SinglePanel, style, _collideWith, "Collide With",
                WithMixedOption(new string[] { MapObjectCollideWith.Entities, MapObjectCollideWith.Characters, MapObjectCollideWith.Titans, MapObjectCollideWith.Humans, MapObjectCollideWith.Projectiles, MapObjectCollideWith.Hitboxes,
                    MapObjectCollideWith.MapObjects, MapObjectCollideWith.All }, "CollideWith", _collideWith), elementHeight: 30f, onDropdownOptionSelect: () => OnChange());
            ElementFactory.CreateDropdownSetting(SinglePanel, style, _physicsMaterial, "Physics Material",
                WithMixedOption(new string[] { MapObjectPhysicsMaterial.Default, MapObjectPhysicsMaterial.Ice }, "PhysicsMaterial", _physicsMaterial), elementHeight: 30f, onDropdownOptionSelect: () => OnChange());
            CreateHorizontalDivider(SinglePanel);
            ElementFactory.CreateToggleSetting(SinglePanel, style, _visible, "Visible", elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnChange());
            ElementFactory.CreateDropdownSetting(SinglePanel, style, _shader, "Shader",
               WithMixedOption(new string[] { MapObjectShader.Default, MapObjectShader.Basic, MapObjectShader.Transparent, MapObjectShader.Reflective, MapObjectShader.DefaultNoTint, MapObjectShader.DefaultTiled }, "Shader", _shader),
               elementHeight: 30f, onDropdownOptionSelect: () => OnChange());
            if (_shader.Value != MapObjectShader.DefaultNoTint)
            {
                ElementFactory.CreateColorSetting(SinglePanel, style, _color, "Color", _menu.ColorPickPopup, onChangeColor: () => OnChange(),
               elementHeight: 25f);
            }
            if (MapObjectShader.IsLegacyShader(_shader.Value))
            {
                group = ElementFactory.CreateHorizontalGroup(SinglePanel, 20f, TextAnchor.MiddleLeft).transform;
                ElementFactory.CreateInputSetting(SinglePanel, style, _tilingX, "Tiling X", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
                ElementFactory.CreateInputSetting(SinglePanel, style, _tilingY, "Tiling Y", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            }
            else if (_shader.Value == MapObjectShader.DefaultTiled)
            {
                ElementFactory.CreateInputSetting(SinglePanel, style, _tilingX, "Tiling X", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
                ElementFactory.CreateInputSetting(SinglePanel, style, _tilingY, "Tiling Y", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            }
            else if (_shader.Value != MapObjectShader.Default && _shader.Value != MapObjectShader.DefaultNoTint && _shader.Value != MapObjectShader.DefaultTiled)
            {
                if (_shader.Value == MapObjectShader.Reflective)
                {
                    ElementFactory.CreateColorSetting(SinglePanel, style, _reflectColor, "Reflect color", _menu.ColorPickPopup,
                        onChangeColor: () => OnChange(), elementHeight: 25f);
                }
                group = ElementFactory.CreateHorizontalGroup(SinglePanel, 20f, TextAnchor.MiddleLeft).transform;
                var label = ElementFactory.CreateDefaultLabel(group, style, "Texture", alignment: TextAnchor.MiddleLeft);
                label.GetComponent<LayoutElement>().preferredWidth = 160f;
                ElementFactory.CreateDefaultButton(group, style, _texture.Value, onClick: () => OnButtonClick("Texture"));
                ElementFactory.CreateInputSetting(SinglePanel, style, _tilingX, "Tiling X", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
                ElementFactory.CreateInputSetting(SinglePanel, style, _tilingY, "Tiling Y", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
                ElementFactory.CreateInputSetting(SinglePanel, style, _offsetX, "Offset X", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
                ElementFactory.CreateInputSetting(SinglePanel, style, _offsetY, "Offset Y", elementWidth: inputWidth, elementHeight: 35f, onEndEdit: () => OnChange());
            }
            CreateHorizontalDivider(SinglePanel);

            if (_mapObjects.Count == 1)
            {
                for (int i = 0; i < _components.Count; i++)
                {
                ElementFactory.CreateDefaultLabel(SinglePanel, style, _componentNames[i]);
                var settings = _components[i];
                string description = CustomLogicManager.GetModeDescription(settings);
                if (description != "")
                    ElementFactory.CreateDefaultLabel(SinglePanel, style, description, alignment: TextAnchor.MiddleLeft);
                var tooltips = new Dictionary<string, string>();
                var dropboxes = new Dictionary<string, string[]>();
                foreach (string key in settings.Keys)
                {
                    BaseSetting setting = settings[key];
                    if (key.EndsWith("Tooltip") && setting is StringSetting)
                        tooltips[key.Substring(0, key.Length - 7)] = ((StringSetting)setting).Value;
                    else if (key.EndsWith("Dropbox") && setting is StringSetting)
                    {
                        List<string> options = new List<string>();
                        foreach (string option in ((StringSetting)setting).Value.Split(','))
                            options.Add(option.Trim());
                        if (options.Count == 0)
                            options.Add("None");
                        dropboxes[key.Substring(0, key.Length - 7)] = options.ToArray();
                    }
                }
                foreach (string key in settings.Keys)
                {
                    var setting = settings[key];
                    if (key == "Description")
                        continue;
                    if (key.EndsWith("Tooltip") && setting is StringSetting)
                        continue;
                    if (key.EndsWith("Dropbox") && setting is StringSetting)
                        continue;
                    string tooltip = "";
                    if (tooltips.ContainsKey(key))
                        tooltip = tooltips[key];
                    if (dropboxes.ContainsKey(key) && setting is StringSetting)
                        ElementFactory.CreateDropdownSetting(SinglePanel, style, setting, key, dropboxes[key], tooltip, elementWidth: 140f, elementHeight: 35f, onDropdownOptionSelect: () => OnChange());
                    else if (setting is BoolSetting)
                        ElementFactory.CreateToggleSetting(SinglePanel, style, setting, key, tooltip, onValueChanged: () => OnChange());
                    else if (setting is StringSetting || setting is FloatSetting || setting is IntSetting)
                        ElementFactory.CreateInputSetting(SinglePanel, style, setting, key, tooltip, elementWidth: 140f, elementHeight: 35f, onEndEdit: () => OnChange());
                    else if (setting is ColorSetting)
                        ElementFactory.CreateColorSetting(SinglePanel, style, setting, key, _menu.ColorPickPopup, tooltip, onChangeColor: () => OnChange());
                    else if (setting is Vector3Setting)
                        ElementFactory.CreateVector3Setting(SinglePanel, style, setting, key, _menu.Vector3Popup, tooltip, onChangeVector: () => OnChange());
                }
                string name = "DeleteComponent" + i.ToString();
                ElementFactory.CreateDefaultButton(SinglePanel, style, "Delete", onClick: () => OnButtonClick(name));
                CreateHorizontalDivider(SinglePanel);
            }
            ElementFactory.CreateDefaultButton(SinglePanel, style, "Add Component", onClick: () => OnButtonClick("AddComponent"));
            }
            ApplyMixedIndicators();
            CaptureSyncedValues();
            SinglePanel.gameObject.SetActive(false);
            StartCoroutine(WaitAndEnablePanel());
        }

        private IEnumerator WaitAndEnablePanel()
        {
            yield return new WaitForEndOfFrame();
            SinglePanel.gameObject.SetActive(true);
        }

        private void OnButtonClick(string name)
        {
            if (_menu.IsPopupActive())
                return;
            if (name == "Texture")
                _menu.TexturePopup.Show();
            else if (name == "AddComponent")
            {
                _menu.AddComponentPopup.Show(onLoad: () => OnAddComponent());
            }
            else if (name.StartsWith("DeleteComponent"))
            {
                int index = int.Parse(name.Substring("DeleteComponent".Length));
                _menu.ConfirmPopup.Show("Delete this component?", onConfirm: () => OnDeleteComponent(index));
            }
        }

        private void OnAddComponent()
        {
            string component = _menu.AddComponentPopup.FinishSetting.Value;
            if (!_componentNames.Contains(component))
            {
                _components.Add(_gameManager.LogicEvaluator.GetComponentSettings(component, new List<string>()));
                _componentNames.Add(component);
            }
            OnChange();
        }

        private void OnDeleteComponent(int index)
        {
            _components.RemoveAt(index);
            _componentNames.RemoveAt(index);
            OnChange();
        }

        private string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            
            // Remove characters used in MapScript CSV parsing
            char[] invalidChars = new char[] { ',', ';', '/', '\\', '\n', '\r', '|', ':' };
            foreach (char c in invalidChars)
            {
                name = name.Replace(c.ToString(), string.Empty);
            }
            
            // Limit to 255 characters
            if (name.Length > 255)
                name = name.Substring(0, 255);
            
            return name.Trim();
        }

        public void SyncSettings()
        {
            var script = (MapScriptSceneObject)_mapObject.ScriptObject;
            _name.Value = SanitizeName(script.Name);
            _active.Value = script.Active;
            _static.Value = script.Static;
            _networked.Value = script.Networked;
            _parent.Value = script.Parent;
            _visible.Value = script.Visible;
            var position = script.GetPosition();
            _positionX.Value = position.x;
            _positionY.Value = position.y;
            _positionZ.Value = position.z;
            var rotation = script.GetRotation();
            _rotationX.Value = rotation.x;
            _rotationY.Value = rotation.y;
            _rotationZ.Value = rotation.z;
            var scale = script.GetScale();
            _scaleX.Value = scale.x;
            _scaleY.Value = scale.y;
            _scaleZ.Value = scale.z;
            _collideMode.Value = script.CollideMode;
            _collideWith.Value = script.CollideWith;
            _physicsMaterial.Value = script.PhysicsMaterial;
            _shader.Value = script.Material.Shader;
            _color.Value = script.Material.Color;
            _reflectColor.Value = new Color255();
            _texture.Value = "Misc/None";
            _tilingX.Value = 1f;
            _tilingY.Value = 1f;
            _offsetX.Value = 0f;
            _offsetY.Value = 0f;
            if (script.Material is MapScriptLegacyMaterial)
            {
                var material = (MapScriptLegacyMaterial)script.Material;
                _tilingX.Value = material.Tiling.x;
                _tilingY.Value = material.Tiling.y;
            }
            else if (script.Material is MapScriptDefaultTiledMaterial)
            {
                var material = (MapScriptDefaultTiledMaterial)script.Material;
                _tilingX.Value = material.Tiling.x;
                _tilingY.Value = material.Tiling.y;
            }
            else if (typeof(MapScriptBasicMaterial).IsAssignableFrom(script.Material.GetType()))
            {
                var material = (MapScriptBasicMaterial)script.Material;
                _texture.Value = material.Texture;
                _tilingX.Value = material.Tiling.x;
                _tilingY.Value = material.Tiling.y;
                _offsetX.Value = material.Offset.x;
                _offsetY.Value = material.Offset.y;
                if (script.Material is MapScriptReflectiveMaterial)
                    _reflectColor.Value = ((MapScriptReflectiveMaterial)script.Material).ReflectColor;
            }

            // Detect mixed values across all selected objects
            for (int i = 1; i < _mapObjects.Count; i++)
            {
                var s = (MapScriptSceneObject)_mapObjects[i].ScriptObject;
                if (SanitizeName(s.Name) != _name.Value) _mixedFields.Add("Name");
                if (s.Active != _active.Value) _mixedFields.Add("Active");
                if (s.Static != _static.Value) _mixedFields.Add("Static");
                if (s.Networked != _networked.Value) _mixedFields.Add("Networked");
                if (s.Visible != _visible.Value) _mixedFields.Add("Visible");
                if (s.Parent != _parent.Value) _mixedFields.Add("Parent");
                var p = s.GetPosition();
                if (p.x != _positionX.Value) _mixedFields.Add("PositionX");
                if (p.y != _positionY.Value) _mixedFields.Add("PositionY");
                if (p.z != _positionZ.Value) _mixedFields.Add("PositionZ");
                var r = s.GetRotation();
                if (r.x != _rotationX.Value) _mixedFields.Add("RotationX");
                if (r.y != _rotationY.Value) _mixedFields.Add("RotationY");
                if (r.z != _rotationZ.Value) _mixedFields.Add("RotationZ");
                var sc = s.GetScale();
                if (sc.x != _scaleX.Value) _mixedFields.Add("ScaleX");
                if (sc.y != _scaleY.Value) _mixedFields.Add("ScaleY");
                if (sc.z != _scaleZ.Value) _mixedFields.Add("ScaleZ");
                if (s.CollideMode != _collideMode.Value) _mixedFields.Add("CollideMode");
                if (s.CollideWith != _collideWith.Value) _mixedFields.Add("CollideWith");
                if (s.PhysicsMaterial != _physicsMaterial.Value) _mixedFields.Add("PhysicsMaterial");
                if (s.Material.Shader != _shader.Value) _mixedFields.Add("Shader");
                if (!ColorEquals(s.Material.Color, _color.Value)) _mixedFields.Add("Color");
            }

            // Apply "--" sentinel for mixed string fields
            if (_mixedFields.Contains("Name")) _name.Value = MixedStringValue;
            if (_mixedFields.Contains("Texture")) _texture.Value = MixedStringValue;

            _components = new List<Dictionary<string, BaseSetting>>();
            _componentNames = new List<string>();
            if (_mapObjects.Count == 1)
            {
                foreach (var component in ((MapScriptSceneObject)_mapObject.ScriptObject).Components)
                {
                    _componentNames.Add(component.ComponentName);
                    _components.Add(_gameManager.LogicEvaluator.GetComponentSettings(component.ComponentName, component.Parameters));
                }
            }
            SyncSettingElements();
        }

        private static bool ColorEquals(Color255 a, Color255 b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
        }

        private string[] WithMixedOption(string[] options, string fieldKey, StringSetting setting)
        {
            if (!_mixedFields.Contains(fieldKey))
                return options;
            setting.Value = MixedStringValue;
            var list = new List<string>(options);
            list.Insert(0, MixedStringValue);
            return list.ToArray();
        }

        private void CaptureSyncedValues()
        {
            _syncedSerialized.Clear();
            _syncedSerialized["Name"] = _name.Value;
            _syncedSerialized["Active"] = _active.Value.ToString();
            _syncedSerialized["Static"] = _static.Value.ToString();
            _syncedSerialized["Networked"] = _networked.Value.ToString();
            _syncedSerialized["Visible"] = _visible.Value.ToString();
            _syncedSerialized["Parent"] = _parent.Value.ToString();
            _syncedSerialized["PositionX"] = _positionX.Value.ToString("R");
            _syncedSerialized["PositionY"] = _positionY.Value.ToString("R");
            _syncedSerialized["PositionZ"] = _positionZ.Value.ToString("R");
            _syncedSerialized["RotationX"] = _rotationX.Value.ToString("R");
            _syncedSerialized["RotationY"] = _rotationY.Value.ToString("R");
            _syncedSerialized["RotationZ"] = _rotationZ.Value.ToString("R");
            _syncedSerialized["ScaleX"] = _scaleX.Value.ToString("R");
            _syncedSerialized["ScaleY"] = _scaleY.Value.ToString("R");
            _syncedSerialized["ScaleZ"] = _scaleZ.Value.ToString("R");
            _syncedSerialized["CollideMode"] = _collideMode.Value;
            _syncedSerialized["CollideWith"] = _collideWith.Value;
            _syncedSerialized["PhysicsMaterial"] = _physicsMaterial.Value;
            _syncedSerialized["Shader"] = _shader.Value;
            _syncedSerialized["Color"] = SerializeSetting(_color);
            _syncedSerialized["ReflectColor"] = SerializeSetting(_reflectColor);
            _syncedSerialized["Texture"] = _texture.Value;
            _syncedSerialized["TilingX"] = _tilingX.Value.ToString("R");
            _syncedSerialized["TilingY"] = _tilingY.Value.ToString("R");
            _syncedSerialized["OffsetX"] = _offsetX.Value.ToString("R");
            _syncedSerialized["OffsetY"] = _offsetY.Value.ToString("R");
        }

        private bool IsModified(string key, string current)
        {
            return _syncedSerialized.TryGetValue(key, out var old) && old != current;
        }

        private void ApplyMixedIndicators()
        {
            var settingToKey = new Dictionary<BaseSetting, string>
            {
                { _positionX, "PositionX" },
                { _positionY, "PositionY" },
                { _positionZ, "PositionZ" },
                { _rotationX, "RotationX" },
                { _rotationY, "RotationY" },
                { _rotationZ, "RotationZ" },
                { _scaleX, "ScaleX" },
                { _scaleY, "ScaleY" },
                { _scaleZ, "ScaleZ" },
                { _tilingX, "TilingX" },
                { _tilingY, "TilingY" },
                { _offsetX, "OffsetX" },
                { _offsetY, "OffsetY" },
                { _parent, "Parent" },
                { _active, "Active" },
                { _static, "Static" },
                { _networked, "Networked" },
                { _visible, "Visible" },
            };
            var settingField = typeof(BaseSettingElement).GetField("_setting",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (settingField == null)
                return;
            foreach (var input in SinglePanel.GetComponentsInChildren<InputSettingElement>(true))
            {
                var setting = settingField.GetValue(input) as BaseSetting;
                if (setting == null)
                    continue;
                if (settingToKey.TryGetValue(setting, out string key) && _mixedFields.Contains(key))
                {
                    var field = input.transform.Find("InputField")?.GetComponent<InputField>();
                    if (field != null)
                        field.text = MixedStringValue;
                }
            }
            foreach (var toggleElement in SinglePanel.GetComponentsInChildren<ToggleSettingElement>(true))
            {
                var setting = settingField.GetValue(toggleElement) as BaseSetting;
                if (setting == null)
                    continue;
                if (settingToKey.TryGetValue(setting, out string key) && _mixedFields.Contains(key))
                {
                    var toggle = toggleElement.transform.Find("Toggle")?.GetComponent<Toggle>();
                    if (toggle == null)
                        continue;
                    var background = toggle.transform.Find("Background");
                    var checkmark = background != null ? background.Find("Checkmark")?.GetComponent<Image>() : null;
                    if (checkmark == null)
                        continue;
                    // Show a "mixed" indicator: hide the checkmark and overlay a "--" label.
                    toggle.SetIsOnWithoutNotify(false);
                    checkmark.enabled = false;
                    var labelGO = new GameObject("MixedLabel", typeof(RectTransform), typeof(Text));
                    labelGO.transform.SetParent(background, false);
                    var labelRect = (RectTransform)labelGO.transform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;
                    var labelText = labelGO.GetComponent<Text>();
                    labelText.text = MixedStringValue;
                    labelText.alignment = TextAnchor.MiddleCenter;
                    labelText.color = checkmark.color;
                    labelText.raycastTarget = false;
                    labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    labelText.fontSize = Mathf.Max(10, (int)(background.GetComponent<RectTransform>().rect.height * 0.9f));
                    labelText.fontStyle = FontStyle.Bold;
                    string capturedKey = key;
                    // Remove the overlay once the user toggles and force the field to be treated
                    // as modified so the new value is applied to all objects.
                    toggle.onValueChanged.AddListener((bool _) =>
                    {
                        if (labelGO != null)
                            Destroy(labelGO);
                        checkmark.enabled = true;
                        _syncedSerialized.Remove(capturedKey);
                        _mixedFields.Remove(capturedKey);
                    });
                }
            }
        }

        public void OnSelectTexture(string texture)
        {
            _texture.Value = BuiltinMapTextures.AllTextures[texture].Texture;
            OnChange();
        }

        public void OnChange()
        {
            bool needRefresh = false;
            bool nameChange = false;

            // Determine which fields were modified by comparing current values to the synced snapshot.
            bool modName = IsModified("Name", _name.Value);
            bool modActive = IsModified("Active", _active.Value.ToString());
            bool modStatic = IsModified("Static", _static.Value.ToString());
            bool modNetworked = IsModified("Networked", _networked.Value.ToString());
            bool modVisible = IsModified("Visible", _visible.Value.ToString());
            bool modParent = IsModified("Parent", _parent.Value.ToString());
            bool modPosX = IsModified("PositionX", _positionX.Value.ToString("R"));
            bool modPosY = IsModified("PositionY", _positionY.Value.ToString("R"));
            bool modPosZ = IsModified("PositionZ", _positionZ.Value.ToString("R"));
            bool modRotX = IsModified("RotationX", _rotationX.Value.ToString("R"));
            bool modRotY = IsModified("RotationY", _rotationY.Value.ToString("R"));
            bool modRotZ = IsModified("RotationZ", _rotationZ.Value.ToString("R"));
            bool modScaleX = IsModified("ScaleX", _scaleX.Value.ToString("R"));
            bool modScaleY = IsModified("ScaleY", _scaleY.Value.ToString("R"));
            bool modScaleZ = IsModified("ScaleZ", _scaleZ.Value.ToString("R"));
            bool modCollideMode = IsModified("CollideMode", _collideMode.Value);
            bool modCollideWith = IsModified("CollideWith", _collideWith.Value);
            bool modPhysicsMaterial = IsModified("PhysicsMaterial", _physicsMaterial.Value);
            bool modShader = IsModified("Shader", _shader.Value);
            bool modColor = IsModified("Color", SerializeSetting(_color));
            bool modReflectColor = IsModified("ReflectColor", SerializeSetting(_reflectColor));
            bool modTexture = IsModified("Texture", _texture.Value);
            bool modTilingX = IsModified("TilingX", _tilingX.Value.ToString("R"));
            bool modTilingY = IsModified("TilingY", _tilingY.Value.ToString("R"));
            bool modOffsetX = IsModified("OffsetX", _offsetX.Value.ToString("R"));
            bool modOffsetY = IsModified("OffsetY", _offsetY.Value.ToString("R"));

            // Guard against users leaving "--" in mixed string fields
            if (modName && _name.Value == MixedStringValue) modName = false;
            if (modTexture && _texture.Value == MixedStringValue) modTexture = false;

            List<MapObject> positionObjects = new List<MapObject>();
            List<MapObject> rotationObjects = new List<MapObject>();
            List<MapObject> scaleObjects = new List<MapObject>();

            foreach (var mapObject in _mapObjects)
            {
                var script = (MapScriptSceneObject)mapObject.ScriptObject;
                if (modName)
                {
                    if (script.Name != _name.Value)
                        nameChange = true;
                    script.Name = _name.Value;
                }
                if (modActive) script.Active = _active.Value;
                if (modStatic) script.Static = _static.Value;
                if (modNetworked) script.Networked = _networked.Value;
                if (modVisible) script.Visible = _visible.Value;
                if (modParent)
                {
                    int p = _parent.Value;
                    if (p > 0 && (!MapLoader.IdToMapObject.ContainsKey(p) || p == script.Id || MapLoader.IdToMapObject[p].Parent == script.Id))
                        p = 0;
                    script.Parent = p;
                }

                if (modPosX || modPosY || modPosZ)
                {
                    var current = script.GetPosition();
                    var np = new Vector3(
                        modPosX ? _positionX.Value : current.x,
                        modPosY ? _positionY.Value : current.y,
                        modPosZ ? _positionZ.Value : current.z);
                    if (np != current)
                    {
                        mapObject.GameObject.transform.position = np;
                        positionObjects.Add(mapObject);
                    }
                }
                if (modRotX || modRotY || modRotZ)
                {
                    var current = script.GetRotation();
                    var nr = new Vector3(
                        modRotX ? _rotationX.Value : current.x,
                        modRotY ? _rotationY.Value : current.y,
                        modRotZ ? _rotationZ.Value : current.z);
                    if (nr != current)
                    {
                        mapObject.GameObject.transform.rotation = Quaternion.Euler(nr);
                        rotationObjects.Add(mapObject);
                    }
                }
                if (modScaleX || modScaleY || modScaleZ)
                {
                    var current = script.GetScale();
                    var ns = new Vector3(
                        modScaleX ? _scaleX.Value : current.x,
                        modScaleY ? _scaleY.Value : current.y,
                        modScaleZ ? _scaleZ.Value : current.z);
                    if (ns != current)
                    {
                        mapObject.GameObject.transform.localScale = Util.MultiplyVectors(mapObject.BaseScale, ns);
                        scaleObjects.Add(mapObject);
                    }
                }

                if (modCollideMode) script.CollideMode = _collideMode.Value;
                if (modCollideWith) script.CollideWith = _collideWith.Value;
                if (modPhysicsMaterial) script.PhysicsMaterial = _physicsMaterial.Value;

                if (modShader && _shader.Value != script.Material.Shader)
                {
                    if (_shader.Value == MapObjectShader.Default || _shader.Value == MapObjectShader.DefaultNoTint)
                    {
                        script.Material = new MapScriptBaseMaterial();
                    }
                    else if (_shader.Value == MapObjectShader.DefaultTiled)
                    {
                        script.Material = new MapScriptDefaultTiledMaterial();
                    }
                    else if (_shader.Value == MapObjectShader.Basic || _shader.Value == MapObjectShader.Transparent)
                        script.Material = new MapScriptBasicMaterial();
                    else if (_shader.Value == MapObjectShader.Reflective)
                        script.Material = new MapScriptReflectiveMaterial();
                    else
                        script.Material = new MapScriptLegacyMaterial();
                    script.Material.Shader = _shader.Value;
                    needRefresh = true;
                }
                if (modColor) script.Material.Color = _color.Value;
                if (script.Material is MapScriptLegacyMaterial)
                {
                    var material = (MapScriptLegacyMaterial)script.Material;
                    if (modTilingX || modTilingY)
                        material.Tiling = new Vector2(
                            modTilingX ? _tilingX.Value : material.Tiling.x,
                            modTilingY ? _tilingY.Value : material.Tiling.y);
                }
                else if (script.Material is MapScriptDefaultTiledMaterial)
                {
                    var material = (MapScriptDefaultTiledMaterial)script.Material;
                    if (modTilingX || modTilingY)
                        material.Tiling = new Vector2(
                            modTilingX ? _tilingX.Value : material.Tiling.x,
                            modTilingY ? _tilingY.Value : material.Tiling.y);
                }
                else if (typeof(MapScriptBasicMaterial).IsAssignableFrom(script.Material.GetType()))
                {
                    var material = (MapScriptBasicMaterial)script.Material;
                    if (modTexture && material.Texture != _texture.Value)
                    {
                        needRefresh = true;
                        material.Texture = _texture.Value;
                    }
                    if (modTilingX || modTilingY)
                        material.Tiling = new Vector2(
                            modTilingX ? _tilingX.Value : material.Tiling.x,
                            modTilingY ? _tilingY.Value : material.Tiling.y);
                    if (modOffsetX || modOffsetY)
                        material.Offset = new Vector2(
                            modOffsetX ? _offsetX.Value : material.Offset.x,
                            modOffsetY ? _offsetY.Value : material.Offset.y);
                    if (modReflectColor && material is MapScriptReflectiveMaterial)
                        ((MapScriptReflectiveMaterial)material).ReflectColor = _reflectColor.Value;
                }

                // Components are only editable in single-select mode
                if (_mapObjects.Count == 1)
                {
                    if (script.Components.Count != _componentNames.Count)
                        needRefresh = true;
                    var oldComponentDict = new Dictionary<string, List<string>>();
                    foreach (var component in script.Components)
                        oldComponentDict.Add(component.ComponentName, component.Parameters);
                    List<MapScriptComponent> newComponents = new List<MapScriptComponent>();
                    for (int i = 0; i < _componentNames.Count; i++)
                    {
                        var newParameters = new List<string>();
                        var settings = _components[i];
                        HashSet<string> parsedParameters = new HashSet<string>();
                        foreach (string key in settings.Keys)
                        {
                            parsedParameters.Add(key);
                            if (key == "Description")
                                continue;
                            if (key.EndsWith("Tooltip") && settings[key] is StringSetting)
                                continue;
                            if (key.EndsWith("Dropbox") && settings[key] is StringSetting)
                                continue;
                            newParameters.Add(key + ":" + SerializeSetting(settings[key]));
                        }
                        if (oldComponentDict.ContainsKey(_componentNames[i]))
                        {
                            var oldParameters = oldComponentDict[_componentNames[i]];
                            foreach (string oldParameter in oldParameters)
                            {
                                string key = oldParameter.Split(':')[0];
                                if (!parsedParameters.Contains(key))
                                    newParameters.Add(oldParameter);
                            }
                        }
                        var component = new MapScriptComponent();
                        component.ComponentName = _componentNames[i];
                        component.Parameters = newParameters;
                        newComponents.Add(component);
                    }
                    script.Components = newComponents;
                }

                MapLoader.SetMaterial(mapObject.GameObject, script.Asset, script.Material, script.Visible, true);
            }

            // Batch transform commands into a single undo step
            List<BaseCommand> transformCommands = new List<BaseCommand>();
            if (positionObjects.Count > 0)
                transformCommands.Add(new TransformPositionCommand(positionObjects));
            if (rotationObjects.Count > 0)
                transformCommands.Add(new TransformRotationCommand(rotationObjects));
            if (scaleObjects.Count > 0)
                transformCommands.Add(new TransformScaleCommand(scaleObjects));
            if (transformCommands.Count == 1)
                _gameManager.NewCommand(transformCommands[0], false);
            else if (transformCommands.Count > 1)
                _gameManager.NewCommand(new CompositeCommand(transformCommands), false);

            // Re-capture synced snapshot so subsequent edits compare against the now-applied state
            CaptureSyncedValues();

            _gameManager.SyncGizmos();
            if (needRefresh)
                _menu.ShowInspector(_mapObjects);
            if (nameChange)
                _menu.SyncHierarchyPanel();
        }

        public string SerializeSetting(BaseSetting setting)
        {
            if (setting is FloatSetting)
                return ((FloatSetting)setting).Value.ToString();
            else if (setting is IntSetting)
                return ((IntSetting)setting).Value.ToString();
            else if (setting is BoolSetting)
                return ((BoolSetting)setting).Value ? "true" : "false";
            else if (setting is StringSetting)
                return ((StringSetting)setting).Value.Replace(',', ' ').Replace(':', ' ').Replace('|', ' ');
            else if (setting is ColorSetting)
            {
                var color = ((ColorSetting)setting).Value;
                return string.Join("/", new string[] { color.R.ToString(), color.G.ToString(), color.B.ToString(), color.A.ToString() });
            }
            else if (setting is Vector3Setting)
            {
                var vector = ((Vector3Setting)setting).Value;
                return string.Join("/", new string[] { vector.x.ToString(), vector.y.ToString(), vector.z.ToString() });
            }
            return string.Empty;
        }
    }
}