using ApplicationManagers;
using GameManagers;
using Map;
using MapEditor;
using Settings;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    class MapEditorBrushPanel : HeadedPanel
    {
        protected override float Width => 400f;
        protected override float Height => 990f;
        protected override string Title => "Brush";
        protected override float TopBarHeight => 0f;
        protected override float BottomBarHeight => 0f;
        protected override float VerticalSpacing => 8f;
        protected override int HorizontalPadding => 20;
        protected override int VerticalPadding => 15;
        protected override bool ScrollBar => true;
        protected override string ThemePanel => "MapEditor";

        private MapEditorGameManager _gameManager;
        private MapEditorMenu _menu;
        private FloatSetting _radius = new FloatSetting(10f);
        private IntSetting _count = new IntSetting(8);
        private BoolSetting _randomize = new BoolSetting(true);
        private StringSetting _mode = new StringSetting("Scatter");
        private IntSetting _gridX = new IntSetting(3);
        private IntSetting _gridY = new IntSetting(1);
        private IntSetting _gridZ = new IntSetting(3);
        private FloatSetting _spacingX = new FloatSetting(5f);
        private FloatSetting _spacingY = new FloatSetting(5f);
        private FloatSetting _spacingZ = new FloatSetting(5f);
        private StringSetting _curveAxis = new StringSetting("None");
        private FloatSetting _curveAmount = new FloatSetting(0f);

        // Scratch settings used while rebuilding per-entry rows.
        private List<FloatSetting> _weight = new List<FloatSetting>();
        private List<BoolSetting> _advanced = new List<BoolSetting>();
        private List<FloatSetting> _scaleMin = new List<FloatSetting>();
        private List<FloatSetting> _scaleMax = new List<FloatSetting>();
        private List<FloatSetting> _yawMin = new List<FloatSetting>();
        private List<FloatSetting> _yawMax = new List<FloatSetting>();
        private List<FloatSetting> _scaleXMin = new List<FloatSetting>();
        private List<FloatSetting> _scaleXMax = new List<FloatSetting>();
        private List<FloatSetting> _scaleYMin = new List<FloatSetting>();
        private List<FloatSetting> _scaleYMax = new List<FloatSetting>();
        private List<FloatSetting> _scaleZMin = new List<FloatSetting>();
        private List<FloatSetting> _scaleZMax = new List<FloatSetting>();
        private List<FloatSetting> _rotXMin = new List<FloatSetting>();
        private List<FloatSetting> _rotXMax = new List<FloatSetting>();
        private List<FloatSetting> _rotYMin = new List<FloatSetting>();
        private List<FloatSetting> _rotYMax = new List<FloatSetting>();
        private List<FloatSetting> _rotZMin = new List<FloatSetting>();
        private List<FloatSetting> _rotZMax = new List<FloatSetting>();
        // Inspector-style per-entry fields
        private List<BoolSetting> _active = new List<BoolSetting>();
        private List<BoolSetting> _static = new List<BoolSetting>();
        private List<BoolSetting> _visible = new List<BoolSetting>();
        private List<StringSetting> _collideMode = new List<StringSetting>();
        private List<StringSetting> _collideWith = new List<StringSetting>();
        private List<StringSetting> _physicsMaterial = new List<StringSetting>();
        private List<StringSetting> _shader = new List<StringSetting>();
        private List<ColorSetting> _color = new List<ColorSetting>();
        private List<ColorSetting> _reflectColor = new List<ColorSetting>();
        private List<StringSetting> _texture = new List<StringSetting>();
        private List<FloatSetting> _tilingX = new List<FloatSetting>();
        private List<FloatSetting> _tilingY = new List<FloatSetting>();
        private List<FloatSetting> _offsetX = new List<FloatSetting>();
        private List<FloatSetting> _offsetY = new List<FloatSetting>();
        private List<FloatSetting> _yOffset = new List<FloatSetting>();

        public override void Setup(BasePanel parent = null)
        {
            base.Setup(parent);
            _menu = (MapEditorMenu)UIManager.CurrentMenu;
            _gameManager = (MapEditorGameManager)SceneLoader.CurrentGameManager;
        }

        public void Show()
        {
            base.Show();
            Rebuild();
        }

        // Refresh field values in-place without tearing down the UI.
        // Used by viewport scroll handlers that update brush state every frame —
        // a full Rebuild there causes visible flicker and frame hitches.
        public void SyncValues()
        {
            var brush = _gameManager.Brush;
            _radius.Value = brush.Radius;
            _count.Value = brush.Count;
            _randomize.Value = brush.RandomizeEachPlacement;
            _gridX.Value = brush.GridX;
            _gridY.Value = brush.GridY;
            _gridZ.Value = brush.GridZ;
            _spacingX.Value = brush.SpacingX;
            _spacingY.Value = brush.SpacingY;
            _spacingZ.Value = brush.SpacingZ;
            _curveAxis.Value = brush.CurveAxis == 1 ? "X" : brush.CurveAxis == 2 ? "Y" : brush.CurveAxis == 3 ? "Z" : "None";
            _curveAmount.Value = brush.CurveAmount;
            foreach (var el in SinglePanel.GetComponentsInChildren<BaseSettingElement>(true))
                el.SyncElement();
        }

        public void Rebuild()
        {
            foreach (Transform child in SinglePanel)
                Destroy(child.gameObject);
            _weight.Clear();
            _advanced.Clear();
            _scaleMin.Clear();
            _scaleMax.Clear();
            _yawMin.Clear();
            _yawMax.Clear();
            _scaleXMin.Clear(); _scaleXMax.Clear();
            _scaleYMin.Clear(); _scaleYMax.Clear();
            _scaleZMin.Clear(); _scaleZMax.Clear();
            _rotXMin.Clear(); _rotXMax.Clear();
            _rotYMin.Clear(); _rotYMax.Clear();
            _rotZMin.Clear(); _rotZMax.Clear();
            _active.Clear(); _static.Clear(); _visible.Clear();
            _collideMode.Clear(); _collideWith.Clear(); _physicsMaterial.Clear();
            _shader.Clear(); _color.Clear(); _reflectColor.Clear();
            _texture.Clear();
            _tilingX.Clear(); _tilingY.Clear();
            _offsetX.Clear(); _offsetY.Clear();

            var style = new ElementStyle(fontSize: 18, titleWidth: 140f, spacing: 10f, themePanel: ThemePanel);
            var brush = _gameManager.Brush;

            var titleStyle = new ElementStyle(fontSize: 24, titleWidth: 0f, spacing: 10f, themePanel: ThemePanel);
            ElementFactory.CreateDefaultLabel(SinglePanel, titleStyle, "Brush", alignment: TextAnchor.MiddleCenter);
            CreateHorizontalDivider(SinglePanel);

            _radius.Value = brush.Radius;
            _count.Value = brush.Count;
            _randomize.Value = brush.RandomizeEachPlacement;
            _mode.Value = brush.Mode == BrushMode.Grid ? "Grid" : "Scatter";
            _gridX.Value = brush.GridX;
            _gridY.Value = brush.GridY;
            _gridZ.Value = brush.GridZ;
            _spacingX.Value = brush.SpacingX;
            _spacingY.Value = brush.SpacingY;
            _spacingZ.Value = brush.SpacingZ;
            _curveAxis.Value = brush.CurveAxis == 1 ? "X" : brush.CurveAxis == 2 ? "Y" : brush.CurveAxis == 3 ? "Z" : "None";
            _curveAmount.Value = brush.CurveAmount;

            ElementFactory.CreateDropdownSetting(SinglePanel, style, _mode, "Mode",
                new string[] { "Scatter", "Grid" },
                elementHeight: 35f, onDropdownOptionSelect: () => OnModeChanged());

            if (brush.Mode == BrushMode.Grid)
            {
                var gridStyle = new ElementStyle(fontSize: 18, titleWidth: 20f, spacing: 6f, themePanel: ThemePanel);
                var spacingStyle = new ElementStyle(fontSize: 18, titleWidth: 40f, spacing: 6f, themePanel: ThemePanel);
                var gridRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                ElementFactory.CreateInputSetting(gridRow, gridStyle, _gridX, "X",
                    elementWidth: 60f, elementHeight: 35f, onEndEdit: () => OnGridChanged());
                ElementFactory.CreateInputSetting(gridRow, gridStyle, _gridY, "Y",
                    elementWidth: 60f, elementHeight: 35f, onEndEdit: () => OnGridChanged());
                ElementFactory.CreateInputSetting(gridRow, gridStyle, _gridZ, "Z",
                    elementWidth: 60f, elementHeight: 35f, onEndEdit: () => OnGridChanged());
                var spacingRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                ElementFactory.CreateInputSetting(spacingRow, spacingStyle, _spacingX, "Sp X",
                    elementWidth: 70f, elementHeight: 35f, onEndEdit: () => OnGridChanged());
                ElementFactory.CreateInputSetting(spacingRow, spacingStyle, _spacingY, "Sp Y",
                    elementWidth: 70f, elementHeight: 35f, onEndEdit: () => OnGridChanged());
                ElementFactory.CreateInputSetting(spacingRow, spacingStyle, _spacingZ, "Sp Z",
                    elementWidth: 70f, elementHeight: 35f, onEndEdit: () => OnGridChanged());
                ElementFactory.CreateDropdownSetting(SinglePanel, style, _curveAxis, "Curve Axis",
                    new string[] { "None", "X", "Y", "Z" },
                    "Press 1/2/3 in the viewport to set curve axis (X/Y/Z). 0 clears it.",
                    elementHeight: 35f, onDropdownOptionSelect: () => OnCurveAxisChanged());
                ElementFactory.CreateInputSetting(SinglePanel, style, _curveAmount, "Curve Amount",
                    "Shift+scroll in the viewport to adjust.",
                    elementWidth: 100f, elementHeight: 35f, onEndEdit: () => OnCurveAmountChanged());
            }
            else
            {
                ElementFactory.CreateInputSetting(SinglePanel, style, _radius, "Radius",
                    "Scroll wheel adjusts radius while the brush is active.",
                    elementWidth: 100f, elementHeight: 35f, onEndEdit: () => OnRadiusChanged());
                ElementFactory.CreateInputSetting(SinglePanel, style, _count, "Density (count)",
                    "How many objects to place per click.",
                    elementWidth: 100f, elementHeight: 35f, onEndEdit: () => OnCountChanged());
                ElementFactory.CreateToggleSetting(SinglePanel, style, _randomize, "Randomize each placement",
                    "Reshuffle positions, scales, rotations, and asset picks every time you click.",
                    elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnRandomizeChanged());
            }

            var topRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 10f, TextAnchor.MiddleLeft).transform;
            ElementFactory.CreateDefaultButton(topRow, style, "Add Asset",
                elementHeight: 35f, onClick: () => OnAddAsset());
            ElementFactory.CreateDefaultButton(topRow, style, "Re-Roll",
                elementHeight: 35f, onClick: () => OnReroll());
            CreateHorizontalDivider(SinglePanel);

            var labelStyle = new ElementStyle(fontSize: 16, titleWidth: 80f, spacing: 4f, themePanel: ThemePanel);
            for (int i = 0; i < brush.Entries.Count; i++)
            {
                int index = i;
                var entry = brush.Entries[i];
                _weight.Add(new FloatSetting(entry.Weight));
                _advanced.Add(new BoolSetting(entry.Advanced));
                _scaleMin.Add(new FloatSetting(entry.ScaleMin));
                _scaleMax.Add(new FloatSetting(entry.ScaleMax));
                _yawMin.Add(new FloatSetting(entry.YawMin));
                _yawMax.Add(new FloatSetting(entry.YawMax));
                _scaleXMin.Add(new FloatSetting(entry.ScaleXMin));
                _scaleXMax.Add(new FloatSetting(entry.ScaleXMax));
                _scaleYMin.Add(new FloatSetting(entry.ScaleYMin));
                _scaleYMax.Add(new FloatSetting(entry.ScaleYMax));
                _scaleZMin.Add(new FloatSetting(entry.ScaleZMin));
                _scaleZMax.Add(new FloatSetting(entry.ScaleZMax));
                _rotXMin.Add(new FloatSetting(entry.RotXMin));
                _rotXMax.Add(new FloatSetting(entry.RotXMax));
                _rotYMin.Add(new FloatSetting(entry.RotYMin));
                _rotYMax.Add(new FloatSetting(entry.RotYMax));
                _rotZMin.Add(new FloatSetting(entry.RotZMin));
                _rotZMax.Add(new FloatSetting(entry.RotZMax));
                _active.Add(new BoolSetting(entry.Active));
                _static.Add(new BoolSetting(entry.Static));
                _visible.Add(new BoolSetting(entry.Visible));
                _collideMode.Add(new StringSetting(entry.CollideMode));
                _collideWith.Add(new StringSetting(entry.CollideWith));
                _physicsMaterial.Add(new StringSetting(entry.PhysicsMaterial));
                _shader.Add(new StringSetting(entry.Shader));
                _color.Add(new ColorSetting(entry.Color));
                _reflectColor.Add(new ColorSetting(entry.ReflectColor));
                _texture.Add(new StringSetting(entry.Texture));
                _tilingX.Add(new FloatSetting(entry.Tiling.x));
                _tilingY.Add(new FloatSetting(entry.Tiling.y));
                _offsetX.Add(new FloatSetting(entry.Offset.x));
                _offsetY.Add(new FloatSetting(entry.Offset.y));
                _yOffset.Add(new FloatSetting(entry.YOffset));

                var header = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                ElementFactory.CreateDefaultLabel(header, labelStyle, entry.AssetName, alignment: TextAnchor.MiddleLeft);
                ElementFactory.CreateDefaultButton(header, labelStyle, "Remove",
                    elementWidth: 80f, elementHeight: 28f, onClick: () => OnRemove(index));

                ElementFactory.CreateInputSetting(SinglePanel, labelStyle, _weight[index], "Weight",
                    elementWidth: 80f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                ElementFactory.CreateToggleSetting(SinglePanel, labelStyle, _advanced[index], "Advanced",
                    "Toggle per-axis scale/rotation ranges and extra inspector fields.",
                    elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnAdvancedChanged(index));

                // Always visible: Active/Static toggles and Collide Mode, placed above scale.
                var asRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 10f, TextAnchor.MiddleLeft).transform;
                ElementFactory.CreateToggleSetting(asRow, labelStyle, _active[index], "Active",
                    elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnEntryChanged(index));
                ElementFactory.CreateToggleSetting(asRow, labelStyle, _static[index], "Static",
                    elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnEntryChanged(index));
                ElementFactory.CreateDropdownSetting(SinglePanel, labelStyle, _collideMode[index], "Collide Mode",
                    new string[] { MapObjectCollideMode.Physical, MapObjectCollideMode.Region, MapObjectCollideMode.None },
                    elementHeight: 30f, onDropdownOptionSelect: () => OnEntryChanged(index));

                if (!entry.Advanced)
                {
                    var scaleRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                    ElementFactory.CreateInputSetting(scaleRow, labelStyle, _scaleMin[index], "Scale Min",
                        elementWidth: 70f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateInputSetting(scaleRow, labelStyle, _scaleMax[index], "Scale Max",
                        elementWidth: 70f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    var rotRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                    ElementFactory.CreateInputSetting(rotRow, labelStyle, _yawMin[index], "Yaw Min",
                        elementWidth: 70f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateInputSetting(rotRow, labelStyle, _yawMax[index], "Yaw Max",
                        elementWidth: 70f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                }
                else
                {
                    ElementFactory.CreateDefaultLabel(SinglePanel, labelStyle, "Scale", alignment: TextAnchor.MiddleLeft);
                    var sxRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                    ElementFactory.CreateInputSetting(sxRow, labelStyle, _scaleXMin[index], "X Min",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateInputSetting(sxRow, labelStyle, _scaleXMax[index], "X Max",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    var syRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                    ElementFactory.CreateInputSetting(syRow, labelStyle, _scaleYMin[index], "Y Min",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateInputSetting(syRow, labelStyle, _scaleYMax[index], "Y Max",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    var szRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                    ElementFactory.CreateInputSetting(szRow, labelStyle, _scaleZMin[index], "Z Min",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateInputSetting(szRow, labelStyle, _scaleZMax[index], "Z Max",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));

                    ElementFactory.CreateDefaultLabel(SinglePanel, labelStyle, "Rotation", alignment: TextAnchor.MiddleLeft);
                    var rxRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                    ElementFactory.CreateInputSetting(rxRow, labelStyle, _rotXMin[index], "X Min",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateInputSetting(rxRow, labelStyle, _rotXMax[index], "X Max",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    var ryRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                    ElementFactory.CreateInputSetting(ryRow, labelStyle, _rotYMin[index], "Y Min",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateInputSetting(ryRow, labelStyle, _rotYMax[index], "Y Max",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    var rzRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                    ElementFactory.CreateInputSetting(rzRow, labelStyle, _rotZMin[index], "Z Min",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateInputSetting(rzRow, labelStyle, _rotZMax[index], "Z Max",
                        elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                }

                // Inspector-style fields: Active/Static toggles, collide dropdowns,
                // visible toggle, shader + color, and shader-dependent options.
                if (entry.Advanced)
                {
                    ElementFactory.CreateInputSetting(SinglePanel, labelStyle, _yOffset[index], "Y Offset",
                        "Vertical offset applied after ground projection. Use a negative value to clip slightly into the surface.",
                        elementWidth: 80f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    ElementFactory.CreateDropdownSetting(SinglePanel, labelStyle, _collideWith[index], "Collide With",
                        new string[] { MapObjectCollideWith.Entities, MapObjectCollideWith.Characters, MapObjectCollideWith.Titans, MapObjectCollideWith.Humans, MapObjectCollideWith.Projectiles, MapObjectCollideWith.Hitboxes, MapObjectCollideWith.MapObjects, MapObjectCollideWith.All },
                        elementHeight: 30f, onDropdownOptionSelect: () => OnEntryChanged(index));
                    ElementFactory.CreateDropdownSetting(SinglePanel, labelStyle, _physicsMaterial[index], "Physics Material",
                        new string[] { MapObjectPhysicsMaterial.Default, MapObjectPhysicsMaterial.Ice },
                        elementHeight: 30f, onDropdownOptionSelect: () => OnEntryChanged(index));
                    ElementFactory.CreateToggleSetting(SinglePanel, labelStyle, _visible[index], "Visible",
                        elementWidth: 25f, elementHeight: 25f, onValueChanged: () => OnEntryChanged(index));
                    ElementFactory.CreateDropdownSetting(SinglePanel, labelStyle, _shader[index], "Shader",
                        new string[] { MapObjectShader.Default, MapObjectShader.Basic, MapObjectShader.Transparent, MapObjectShader.Reflective, MapObjectShader.DefaultNoTint, MapObjectShader.DefaultTiled },
                        elementHeight: 30f, onDropdownOptionSelect: () => OnShaderChanged(index));
                    if (entry.Shader != MapObjectShader.DefaultNoTint)
                    {
                        ElementFactory.CreateColorSetting(SinglePanel, labelStyle, _color[index], "Color", _menu.ColorPickPopup,
                            elementHeight: 25f, onChangeColor: () => OnEntryChanged(index));
                    }
                    if (entry.Shader == MapObjectShader.DefaultTiled || MapObjectShader.IsLegacyShader(entry.Shader))
                    {
                        var tRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                        ElementFactory.CreateInputSetting(tRow, labelStyle, _tilingX[index], "Tiling X",
                            elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                        ElementFactory.CreateInputSetting(tRow, labelStyle, _tilingY[index], "Tiling Y",
                            elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    }
                    else if (entry.Shader == MapObjectShader.Basic || entry.Shader == MapObjectShader.Transparent || entry.Shader == MapObjectShader.Reflective)
                    {
                        if (entry.Shader == MapObjectShader.Reflective)
                        {
                            ElementFactory.CreateColorSetting(SinglePanel, labelStyle, _reflectColor[index], "Reflect color", _menu.ColorPickPopup,
                                elementHeight: 25f, onChangeColor: () => OnEntryChanged(index));
                        }
                        var texRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 10f, TextAnchor.MiddleLeft).transform;
                        var texLabel = ElementFactory.CreateDefaultLabel(texRow, labelStyle, "Texture", alignment: TextAnchor.MiddleLeft);
                        texLabel.GetComponent<LayoutElement>().preferredWidth = 80f;
                        ElementFactory.CreateDefaultButton(texRow, labelStyle, entry.Texture, onClick: () => OnPickTexture(index));
                        var tRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                        ElementFactory.CreateInputSetting(tRow, labelStyle, _tilingX[index], "Tiling X",
                            elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                        ElementFactory.CreateInputSetting(tRow, labelStyle, _tilingY[index], "Tiling Y",
                            elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                        var oRow = ElementFactory.CreateHorizontalGroup(SinglePanel, 6f, TextAnchor.MiddleLeft).transform;
                        ElementFactory.CreateInputSetting(oRow, labelStyle, _offsetX[index], "Offset X",
                            elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                        ElementFactory.CreateInputSetting(oRow, labelStyle, _offsetY[index], "Offset Y",
                            elementWidth: 60f, elementHeight: 30f, onEndEdit: () => OnEntryChanged(index));
                    }
                }
                CreateHorizontalDivider(SinglePanel);
            }

            if (brush.Entries.Count == 0)
                ElementFactory.CreateDefaultLabel(SinglePanel, labelStyle,
                    "No assets selected.\nClick 'Add Asset' to pick one.",
                    alignment: TextAnchor.MiddleLeft);
        }

        private void OnRadiusChanged()
        {
            _gameManager.Brush.SetRadius(_radius.Value);
        }

        private void OnCountChanged()
        {
            _gameManager.Brush.SetCount(_count.Value);
        }

        private void OnRandomizeChanged()
        {
            _gameManager.Brush.RandomizeEachPlacement = _randomize.Value;
        }

        private void OnModeChanged()
        {
            var brush = _gameManager.Brush;
            var newMode = _mode.Value == "Grid" ? BrushMode.Grid : BrushMode.Scatter;
            if (brush.Mode == newMode) return;
            brush.Mode = newMode;
            brush.RegenerateGhosts();
            Rebuild();
        }

        private void OnGridChanged()
        {
            var brush = _gameManager.Brush;
            brush.GridX = Mathf.Max(1, _gridX.Value);
            brush.GridY = Mathf.Max(1, _gridY.Value);
            brush.GridZ = Mathf.Max(1, _gridZ.Value);
            brush.SpacingX = _spacingX.Value;
            brush.SpacingY = _spacingY.Value;
            brush.SpacingZ = _spacingZ.Value;
            brush.RegenerateGhosts();
        }

        private void OnCurveAxisChanged()
        {
            var brush = _gameManager.Brush;
            brush.CurveAxis = _curveAxis.Value == "X" ? 1 : _curveAxis.Value == "Y" ? 2 : _curveAxis.Value == "Z" ? 3 : 0;
            brush.RegenerateGhosts();
        }

        private void OnCurveAmountChanged()
        {
            _gameManager.Brush.CurveAmount = _curveAmount.Value;
            _gameManager.Brush.RegenerateGhosts();
        }

        private void OnAddAsset()
        {
            _gameManager.Brush.AssetSelectMode = true;
            _menu.AddObjectPopup.Show();
        }

        private void OnReroll()
        {
            _gameManager.Brush.RegenerateGhosts();
        }

        private void OnRemove(int index)
        {
            _gameManager.Brush.RemoveEntry(index);
            Rebuild();
        }

        private void OnEntryChanged(int index)
        {
            var brush = _gameManager.Brush;
            if (index >= brush.Entries.Count) return;
            var e = brush.Entries[index];
            e.Weight = Mathf.Max(0f, _weight[index].Value);
            e.ScaleMin = _scaleMin[index].Value;
            e.ScaleMax = _scaleMax[index].Value;
            e.YawMin = _yawMin[index].Value;
            e.YawMax = _yawMax[index].Value;
            e.ScaleXMin = _scaleXMin[index].Value;
            e.ScaleXMax = _scaleXMax[index].Value;
            e.ScaleYMin = _scaleYMin[index].Value;
            e.ScaleYMax = _scaleYMax[index].Value;
            e.ScaleZMin = _scaleZMin[index].Value;
            e.ScaleZMax = _scaleZMax[index].Value;
            e.RotXMin = _rotXMin[index].Value;
            e.RotXMax = _rotXMax[index].Value;
            e.RotYMin = _rotYMin[index].Value;
            e.RotYMax = _rotYMax[index].Value;
            e.RotZMin = _rotZMin[index].Value;
            e.RotZMax = _rotZMax[index].Value;
            e.Active = _active[index].Value;
            e.Static = _static[index].Value;
            e.Visible = _visible[index].Value;
            e.CollideMode = _collideMode[index].Value;
            e.CollideWith = _collideWith[index].Value;
            e.PhysicsMaterial = _physicsMaterial[index].Value;
            e.Shader = _shader[index].Value;
            e.Color = _color[index].Value;
            e.ReflectColor = _reflectColor[index].Value;
            e.Texture = _texture[index].Value;
            e.Tiling = new Vector2(_tilingX[index].Value, _tilingY[index].Value);
            e.Offset = new Vector2(_offsetX[index].Value, _offsetY[index].Value);
            e.YOffset = _yOffset[index].Value;
            brush.RegenerateGhosts();
        }

        private void OnShaderChanged(int index)
        {
            var brush = _gameManager.Brush;
            if (index >= brush.Entries.Count) return;
            if (brush.Entries[index].Shader == _shader[index].Value) return;
            brush.Entries[index].Shader = _shader[index].Value;
            Rebuild();
            brush.RegenerateGhosts();
        }

        private void OnPickTexture(int index)
        {
            _menu.TexturePopupTarget = (texture) =>
            {
                var brush = _gameManager.Brush;
                if (index >= brush.Entries.Count) return;
                brush.Entries[index].Texture = BuiltinMapTextures.AllTextures[texture].Texture;
                Rebuild();
                brush.RegenerateGhosts();
            };
            _menu.TexturePopup.Show();
        }

        private void OnAdvancedChanged(int index)
        {
            var brush = _gameManager.Brush;
            if (index >= brush.Entries.Count) return;
            if (brush.Entries[index].Advanced == _advanced[index].Value) return;
            brush.Entries[index].Advanced = _advanced[index].Value;
            Rebuild();
            brush.RegenerateGhosts();
        }
    }
}
