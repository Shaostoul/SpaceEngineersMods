using Sandbox.Common.ObjectBuilders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using Sandbox.ModAPI;
using VRage;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Utils;
using ProtoBuf;
using Sandbox.Game.EntityComponents;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;
using IMyEntity = VRage.ModAPI.IMyEntity;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using Sandbox.Game.Lights;
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using VRageRender.Lights;
using SpaceEngineers.Game.Entities.Blocks;

namespace BlueKnight.TurretSpotlight
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeMissileTurret), false, "Spotlight_Turret_Large", "Spotlight_Turret_Small", "SmallSpotlight_Turret_Small")]
    public class TurretSpotlight : MyGameLogicComponent
    {
        // constants
        private const string m_EmissiveName = "EmissiveAtlas";
        private const string m_LightConeTextureString = "\\Textures\\Lights\\reflector_large.dds";
        /*private float m_light_radius_min = 10;
        private float m_light_radius_max = 120;*/
        private const float m_light_intensity_min = 0.5f;
        private const float m_light_intensity_max = 5;
        private const float m_light_offset_min = .4f;
        private const float m_light_offset_max = 5;
        private const float m_light_blink_interval_min = 0;
        private const float m_light_blink_interval_max = 30;
        private const float m_light_blink_length_min = 0;
        private const float m_light_blink_length_max = 100;
        private const float m_light_blink_offset_min = 0;
        private const float m_light_blink_offset_max = 100;

        // private memebers
        private MyEntitySubpart m_light_model;
        private MyLight m_light_emmitter;
        private IMyLargeMissileTurret m_turret;
        private IMyInventory m_inventory;
        private MyFlareDefinition m_light_flare;
        private bool m_light_enabled = true;
        private bool m_enabledCheck = false;
        private string m_light_cone_texture_path = "";
        private float GlareQuerySizeDef
        {
            get
            {
                return m_turret.CubeGrid.GridSize * (true ? 0.5f : 0.1f);
            }
        }
        private bool m_init = false;

        // public members

        // terminal values
        private static Color _light_color = new Color(0, 150, 255, 255);
        private Color m_light_color = _light_color;
        public Color Light_Color
        {
            get { return (m_turret != null ? m_light_color : _light_color); }
            set { m_light_color = value; }
        }
        private static float _light_radius = 120;
        private float m_light_radius = _light_radius;
        public float Light_Radius
        {
            get { return (m_turret != null ? m_light_radius : _light_radius); }
            set { m_light_radius = value; }
        }
        private static float _light_intensity = 4;
        private float m_light_intensity = _light_intensity;
        public float Light_Intensity
        {
            get { return (m_turret != null ? m_light_intensity : _light_intensity); }
            set { m_light_intensity = value; }
        }
        private static float _light_offset = 0.5f;
        private float m_light_offset = _light_offset;
        public float Light_Offset
        {
            get { return (m_turret != null ? m_light_offset : _light_offset); }
            set { m_light_offset = value; }
        }
        private static float _light_blink_interval = 0;
        private float m_light_blink_interval = _light_blink_interval;
        public float Light_Blink_Interval
        {
            get { return (m_turret != null ? m_light_blink_interval : _light_blink_interval); }
            set { m_light_blink_interval = value; }
        }
        private static float _light_blink_length = 10;
        private float m_light_blink_length = _light_blink_length;
        public float Light_Blink_Length
        {
            get { return (m_turret != null ? m_light_blink_length : _light_blink_length); }
            set { m_light_blink_length = value; }
        }
        private static float _light_blink_offset = 0;
        private float m_light_blink_offset = _light_blink_offset;
        public float Light_Blink_Offset
        {
            get { return (m_turret != null ? m_light_blink_offset : _light_blink_offset); }
            set { m_light_blink_offset = value; }
        }
        private static float _turret_azimuth = 0;
        private float m_turret_azimuth = _turret_azimuth;
        public float Turret_Azimuth
        {
            get { return (m_turret != null ? m_turret_azimuth : _turret_azimuth); }
            set { m_turret_azimuth = value; }
        }
        private static float _turret_elevation = 0;
        private float m_turret_elevation = _turret_elevation;
        public float Turret_Elevation
        {
            get { return (m_turret != null ? m_turret_elevation : _turret_elevation); }
            set { m_turret_elevation = value; }
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            m_turret = (Container.Entity as IMyLargeMissileTurret);

            string Subtype = (m_turret as IMyTerminalBlock).BlockDefinition.SubtypeId;
            m_enabledCheck = (Subtype == "Spotlight_Turret_Large" || Subtype == "Spotlight_Turret_Small" || Subtype == "SmallSpotlight_Turret_Small");

            if (m_enabledCheck == true)
            {
                Logger.Instance.LogMessage("Init");
                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;


                // event handlers
                m_turret.OnClosing += OnClosing;
                m_light_cone_texture_path = MyAPIGateway.Utilities.GamePaths.ContentPath + m_LightConeTextureString;
                //Logger.Instance.LogMessage("Test Texture Path: " + m_light_cone_texture_path);
            }

        }

        private void LoadTerminalValues(IMyLargeMissileTurret turret, TurretSetting settings)
        {
            // load data
            if (settings != null)
            {

                Logger.Instance.LogDebug("Found settings for block: " + turret.CustomName);
                turret.GameLogic.GetAs<TurretSpotlight>().Light_Color = settings.LightColor;
                turret.GameLogic.GetAs<TurretSpotlight>().Light_Radius = settings.LightRadius;
                turret.GameLogic.GetAs<TurretSpotlight>().Light_Intensity = settings.LightIntensity;
                turret.GameLogic.GetAs<TurretSpotlight>().Light_Offset = settings.LightOffset;
                turret.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Interval = settings.LightBlinkInterval;
                turret.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Length = settings.LightBlinkLength;
                turret.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Offset = settings.LightBlinkOffset;
                turret.GameLogic.GetAs<TurretSpotlight>().Turret_Azimuth = settings.TurretAzimuth;
                turret.GameLogic.GetAs<TurretSpotlight>().Turret_Elevation = settings.TurretElevation;
            }
        }
        private void SetTerminalValues(IMyLargeMissileTurret turret)
        {
            // set loaded data
            SetTurretLightColor(Light_Color);
            SetTurretLightIntensity(Light_Intensity);
            SetTurretLightRadius(Light_Radius);
            SetTurretLightOffset(Light_Offset);
            SetTurretLightBlinkInterval(Light_Blink_Interval);
            SetTurretLightBlinkLength(Light_Blink_Length);
            SetTurretLightBlinkOffset(Light_Blink_Offset);
            SetTurretOrientation(Turret_Azimuth, Turret_Elevation);
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (m_enabledCheck == true)
            {  
                if (!m_init)
                {
                    // ask server for values
                    PlayerInput.Instance.GetTerminalValues(m_turret.EntityId);
                }
            }
        }
        public override void UpdateBeforeSimulation100()
        {
            if (PlayerInput.Instance.isServer)
                AddAmmo();
        }
        public override void UpdateBeforeSimulation()
        {
            if (m_init && (m_light_model == null || m_light_model.Closed) && m_turret.IsFunctional)
            {
                CheckAndAddLight();

                // turn off frame updates
                NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.NONE;

                // re add ammo check
                if (PlayerInput.Instance.isServer)
                {
                    NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_100TH_FRAME;
                }
            }
            /*else if (m_light_emmitter != null)
            {
                Vector4 color = new Vector4(255, 0, 0, 255);
                MySimpleObjectDraw.DrawLine(m_light_emmitter.Position, m_light_emmitter.Position + m_light_emmitter.ReflectorDirection * 10, MyStringId.GetOrCompute("Square"), ref color, .05f);
            }*/
            
        }

        public void InitTerminalValues(TurretSetting settings = null)
        {
            try
            {
                if (!m_init)
                {
                    CreateTerminalControls();
                    // load stored terminal data
                    LoadTerminalValues(m_turret, settings);

                    m_turret.ShowInInventory = false;

                    if (PlayerInput.Instance.isServer)
                    {
                        m_inventory = m_turret.GetInventory() as IMyInventory;

                        NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_100TH_FRAME;
                        PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
                    }

                    m_turret.EnabledChanged += OnEnabledChanged;
                    m_turret.IsWorkingChanged += OnIsWorkingChanged;
                    m_turret.PropertiesChanged += OnPropertiesChanged;
                    if (PlayerInput.Instance.isServer)
                        m_turret.OnMarkForClose += OnMarkForClose;

                    m_init = true;

                    CheckAndAddLight();

                    //NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                }
            }
            catch (Exception e)
            {
                // Do nothing
                Logger.Instance.LogMessage("Init Terminal Values Error: " + e.Message);
                Logger.Instance.LogMessage(e.StackTrace);
                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            }
        }
        private void CheckAndAddLight()
        {
            
            // check if block is initialized
            if (m_init && (m_light_model == null || m_light_model.Closed) && m_turret.IsFunctional)
            {
                Logger.Instance.LogMessage("Setup Subpart");

                // setup subpart
                var turretsub = (Entity as MyEntity).Subparts["MissileTurretBase1"]?.Subparts["MissileTurretBarrels"];
                m_light_model = turretsub;
                m_light_model.OnClose += OnModelClosing;
                m_light_model.PositionComp.OnPositionChanged += OnModelPositionChanged;
                
                // setup light
                MakeLight();

                // set stored terminal data
                SetTerminalValues(m_turret);
                // check light status
                OnEnabledChanged(m_turret);

                Logger.Instance.LogMessage("init turret: " + m_turret.EntityId);
                Logger.Instance.LogMessage("init light model: " + m_light_model.EntityId);
                Logger.Instance.LogMessage("init light: " + m_light_emmitter.RenderObjectID);

                if (PlayerInput.Instance.isServer)
                {
                    AddAmmo();
                    m_turret.GetActionWithName("ShootOnce").Apply(m_turret);
                }
            }
        }

        private void MakeLight()
        {

            // light setup
            m_light_emmitter = new MyLight();     
            m_light_emmitter.Start(m_light_model.PositionComp.GetPosition(), Light_Color, 0, m_turret.DisplayNameText);
            m_light_emmitter.CastShadows = true;
            m_light_emmitter.LightType = MyLightType.SPOTLIGHT;
            m_light_emmitter.LightType = MyLightType.SPOTLIGHT;
            m_light_emmitter.Position = m_light_model.PositionComp.GetPosition() + m_light_model.PositionComp.GetOrientation().Forward * Light_Offset;

            /*m_light_emmitter.Falloff = 0.3f;
            m_light_emmitter.GlossFactor = 1f;*/
            m_light_emmitter.ParentID = m_light_model.Render.GetRenderObjectID();

            /*m_light_emmitter.GlareOn = m_light_emmitter.LightOn;
            m_light_emmitter.GlareQuerySize = GlareQuerySizeDef;
            m_light_emmitter.GlareType = MyGlareTypeEnum.Directional;
            m_light_emmitter.GlareSize = m_light_flare.Size;
            m_light_emmitter.SubGlares = m_light_flare.SubGlares;*/

            //m_light_flare = MyDefinitionManager.Static.GetDefinition(new MyDefinitionId((MyObjectBuilderType)typeof(MyObjectBuilder_FlareDefinition), "NoFlare")) as MyFlareDefinition ?? new MyFlareDefinition();

            //m_light_emmitter.ReflectorTexture = m_light_cone_texture_path;
            m_light_emmitter.ReflectorGlossFactor = 1f;
            m_light_emmitter.ReflectorFalloff = 0.3f;
            m_light_emmitter.ReflectorColor = Light_Color;
            m_light_emmitter.ReflectorIntensity = Light_Intensity;
            m_light_emmitter.ReflectorDirection = m_light_model.PositionComp.GetOrientation().Forward;
            m_light_emmitter.ReflectorUp = m_light_model.PositionComp.GetOrientation().Up;
            m_light_emmitter.ReflectorRange = Light_Radius;

            m_light_emmitter.ReflectorOn = m_turret.IsWorking;
            m_light_emmitter.LightOn = false;

            m_light_emmitter.UpdateLight();

            //m_light_model.Render.NeedsDrawFromParent = true;
        }
     
        private static bool ControlsInited = false;

        private void CreateTerminalControls()
        {
            if (ControlsInited)
                return;

            ControlsInited = true;
            Func<IMyTerminalBlock, bool> enabledCheck = delegate (IMyTerminalBlock b) { return (b.BlockDefinition.SubtypeId == "Spotlight_Turret_Large" || b.BlockDefinition.SubtypeId == "Spotlight_Turret_Small" || b.BlockDefinition.SubtypeId == "SmallSpotlight_Turret_Small"); };

            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalControls_CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter -= TerminalControls_CustomActionGetter;

            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalControls_CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter += TerminalControls_CustomActionGetter;

            // Separator
            var sep = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(string.Empty);
            sep.Visible = enabledCheck;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(sep);

            // Colors
            var label = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, Sandbox.ModAPI.Ingame.IMyLargeTurretBase>("BlueKnight.TurretSpotlight.ColorLabel");
            var lightColor = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, Sandbox.ModAPI.Ingame.IMyLargeTurretBase>("BlueKnight.TurretSpotlight.LightColor");

            // color label
            label.Label = MyStringId.GetOrCompute("Color");
            label.Visible = enabledCheck;
            label.Enabled = enabledCheck;
            label.SupportsMultipleBlocks = true;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(label);

            // Add color sliders
            lightColor.Title = MyStringId.GetOrCompute("Light");
            lightColor.Enabled = enabledCheck;
            lightColor.Visible = enabledCheck;
            lightColor.SupportsMultipleBlocks = true;
            lightColor.Setter = (b, v) =>
            {
                if (!enabledCheck(b)) return;
                b.GameLogic.GetAs<TurretSpotlight>().SetServerTurretLightColor(v);
            };
            lightColor.Getter = (b) => enabledCheck(b) ? b.GameLogic.GetAs<TurretSpotlight>().Light_Color : Color.Black;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(lightColor);

            // Intensity slider
            var intensitySlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, Sandbox.ModAPI.Ingame.IMyLargeTurretBase>("BlueKnight.TurretSpotlight.LightIntensity");
            intensitySlider.Enabled = enabledCheck;
            intensitySlider.Visible = enabledCheck;
            intensitySlider.Title = MyStringId.GetOrCompute("Light Intensity");
            intensitySlider.SetLimits(m_light_intensity_min, m_light_intensity_max);
            intensitySlider.Getter = (b) => enabledCheck(b) ? b.GameLogic.GetAs<TurretSpotlight>().Light_Intensity : 0;
            intensitySlider.Setter = (b, v) =>
            {
                if (!enabledCheck(b)) return;
                b.GameLogic.GetAs<TurretSpotlight>().SetServerTurretLightIntensity(v);
            };
            intensitySlider.Writer = (b, t) => t.AppendFormat("{0:N1}", intensitySlider.Getter(b));
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(intensitySlider);

            // Offset slider
            var offsetSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, Sandbox.ModAPI.Ingame.IMyLargeTurretBase>("BlueKnight.TurretSpotlight.LightOffset");
            offsetSlider.Enabled = enabledCheck;
            offsetSlider.Visible = enabledCheck;
            offsetSlider.Title = MyStringId.GetOrCompute("Light Offset");
            offsetSlider.SetLimits(m_light_offset_min, m_light_offset_max);
            offsetSlider.Getter = (b) => enabledCheck(b) ? b.GameLogic.GetAs<TurretSpotlight>().Light_Offset : 0;
            offsetSlider.Setter = (b, v) =>
            {
                if (!enabledCheck(b)) return;
                b.GameLogic.GetAs<TurretSpotlight>().SetServerTurretLightOffset(v);
            };
            offsetSlider.Writer = (b, t) => t.AppendFormat("{0:N1}", offsetSlider.Getter(b));
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(offsetSlider);

            // Blink Interval slider
            /*var blink_interval_Slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, Sandbox.ModAPI.Ingame.IMyLargeTurretBase>("BlueKnight.TurretSpotlight.LightBlinkInterval");
            blink_interval_Slider.Enabled = enabledCheck;
            blink_interval_Slider.Visible = enabledCheck;
            blink_interval_Slider.Title = MyStringId.GetOrCompute("Light Blink Interval");
            blink_interval_Slider.SetLimits(m_light_blink_interval_min, m_light_blink_interval_max);
            blink_interval_Slider.Getter = (b) => enabledCheck(b) ? b.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Interval : 0;
            blink_interval_Slider.Setter = (b, v) =>
            {
                if (!enabledCheck(b)) return;
                SetTurretLightBlinkInterval(b as IMyLargeMissileTurret, v);
            };
            blink_interval_Slider.Writer = (b, t) => t.AppendFormat("{0:N1}", blink_interval_Slider.Getter(b)).Append(" s");
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(blink_interval_Slider);

            // Blink Length slider
            var blink_length_Slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, Sandbox.ModAPI.Ingame.IMyLargeTurretBase>("BlueKnight.TurretSpotlight.LightBlinkLength");
            blink_length_Slider.Enabled = enabledCheck;
            blink_length_Slider.Visible = enabledCheck;
            blink_length_Slider.Title = MyStringId.GetOrCompute("Light Blink Length");
            blink_length_Slider.SetLimits(m_light_blink_length_min, m_light_blink_length_max);
            blink_length_Slider.Getter = (b) => enabledCheck(b) ? b.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Length : 0;
            blink_length_Slider.Setter = (b, v) =>
            {
                if (!enabledCheck(b)) return;
                SetTurretLightBlinkLength(b as IMyLargeMissileTurret, v);
            };
            blink_length_Slider.Writer = (b, t) => t.AppendFormat("{0:N1}", blink_length_Slider.Getter(b)).Append(" %");
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(blink_length_Slider);

            // Blink Offset slider
            var blink_offset_Slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, Sandbox.ModAPI.Ingame.IMyLargeTurretBase>("BlueKnight.TurretSpotlight.LightBlinkOffset");
            blink_offset_Slider.Enabled = enabledCheck;
            blink_offset_Slider.Visible = enabledCheck;
            blink_offset_Slider.Title = MyStringId.GetOrCompute("Light Blink Offset");
            blink_offset_Slider.SetLimits(m_light_blink_offset_min, m_light_blink_offset_max);
            blink_offset_Slider.Getter = (b) => enabledCheck(b) ? b.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Offset : 0;
            blink_offset_Slider.Setter = (b, v) =>
            {
                if (!enabledCheck(b)) return;
                SetTurretLightBlinkOffset(b as IMyLargeMissileTurret, v);
            };
            blink_offset_Slider.Writer = (b, t) => t.AppendFormat("{0:N1}", blink_offset_Slider.Getter(b)).Append(" %");
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyLargeTurretBase>(blink_offset_Slider);*/

        }

        private static void TerminalControls_CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (block is IMyLargeTurretBase)
            {
                string subtype = (block as IMyLargeTurretBase).BlockDefinition.SubtypeId;
                var itemsToRemove = new List<IMyTerminalAction>();

                foreach (var action in actions)
                {
                    //Logger.Instance.LogMessage("Action: " + action.Id);
                    switch (subtype)
                    {
                        case "Spotlight_Turret_Large":
                        case "Spotlight_Turret_Small":
                        case "SmallSpotlight_Turret_Small":
                            if (
                                action.Id.StartsWith("OnOff") || action.Id.Equals("Control")
                                )
                                break;
                            else
                                itemsToRemove.Add(action);
                            break;
                    }
                }

                foreach (var action in itemsToRemove)
                {
                    actions.Remove(action);
                }
            }
        }

        private static void TerminalControls_CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block is IMyLargeTurretBase)
            {
                string subtype = (block as IMyLargeTurretBase).BlockDefinition.SubtypeId;
                var itemsToRemove = new List<IMyTerminalControl>();
                int separatorsToKeep = 3;

                foreach (var control in controls)
                {
                    switch (subtype)
                    {
                        case "Spotlight_Turret_Large":
                        case "Spotlight_Turret_Small":
                        case "SmallSpotlight_Turret_Small":
                            switch (control.Id)
                            {
                                case "OnOff":
                                case "ShowInTerminal":
                                case "ShowInToolbarConfig":
                                case "Name":
                                case "ShowOnHUD":
                                case "CustomData":
                                case "Control":
                                case "EnableIdleMovement":
                                case "Range":
                                    break;
                                default:
                                    if (control.Id.StartsWith("BlueKnight.TurretSpotlight"))
                                        break;
                                    if (control.Id.StartsWith("Target"))
                                        break;
                                    if (control is IMyTerminalControlSeparator && separatorsToKeep-- > 0)
                                        break;
                                    itemsToRemove.Add(control);
                                    break;
                            }
                            break;
                    }
                }

                foreach (var action in itemsToRemove)
                {
                    controls.Remove(action);
                }
            }
        }

        private void AddAmmo()
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            m_inventory.GetItems(items);
            if (items.Capacity <= 1)
            {
                var ammo = new MyObjectBuilder_AmmoMagazine()
                {
                    SubtypeName = "SpotlightTurretAmmoMagazine"
                };

                m_inventory.AddItems(2, ammo);
                
            }

        }

        // Closing Event Callbacks
        private void OnMarkForClose(IMyEntity obj)
        {
            var ammo = new MyObjectBuilder_AmmoMagazine()
            {
                SubtypeName = "SpotlightTurretAmmoMagazine"
            };
            IMyInventory inv = obj.GameLogic.GetAs<TurretSpotlight>().m_inventory;
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inv.GetItems(items);
            MyFixedPoint amount = 0;
            foreach (var item in items)
            {
                amount += item.Amount;
            }

            obj.GameLogic.GetAs<TurretSpotlight>().m_inventory.RemoveItemsOfType(amount, ammo);
        }
        private void OnClosing(IMyEntity obj)
        {
            NeedsUpdate = MyEntityUpdateEnum.NONE;
            (obj as IMyFunctionalBlock).OnClosing -= OnClosing;
            if (PlayerInput.Instance.isServer)
                (obj as IMyFunctionalBlock).OnMarkForClose -= OnMarkForClose;
            (obj as IMyFunctionalBlock).EnabledChanged -= OnEnabledChanged;
            (obj as IMyFunctionalBlock).PropertiesChanged -= OnPropertiesChanged;
            (obj as IMyFunctionalBlock).IsWorkingChanged -= OnIsWorkingChanged;
            PlayerInput.Instance.DeleteTerminalValues(obj.EntityId);

        }
        private void OnModelClosing(IMyEntity obj)
        {
            Logger.Instance.LogMessage("Block: " + obj.EntityId + " Model Closing");
            if (m_light_emmitter != null)
                m_light_emmitter.Clear();
            // close the child grid attached to the subpart    
            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
            (obj as MyEntitySubpart).OnClose -= OnClosing;
            (obj as MyEntitySubpart).PositionComp.OnPositionChanged -= OnModelPositionChanged;

        }
        private void OnModelPositionChanged(MyPositionComponentBase pos)
        {
            // save turret position
            GetTurretOrientation();
            //Logger.Instance.LogMessage("Forward:" + pos.GetOrientation().Forward.ToString());
            // update light
            m_light_emmitter.ReflectorDirection = pos.GetOrientation().Forward;
            m_light_emmitter.ReflectorUp = pos.GetOrientation().Up;
            m_light_emmitter.Position = pos.GetPosition() + pos.GetOrientation().Forward * Light_Offset;

            m_light_emmitter.UpdateLight();
        }

        // Change Event Callbacks 
        private void OnEnabledChanged(IMyTerminalBlock block)
        {
            if (m_turret.IsBeingHacked)
            {
                
            }
            if (block == null || block.Closed)
                return;
            // change light state
            m_light_enabled = !IsTurretLightOnOff();
        }
        private void OnPropertiesChanged(IMyTerminalBlock turret)
        {
           
            float range = turret.GetValue<float>("Range");
            if (Light_Radius != (range + 20))
            {
                // set radius to aiming radius + 20
                SetServerTurretLightRadius(range + 20);
            }           
        }
        private void OnIsWorkingChanged(IMyCubeBlock block)
        {
            if (block == null || block.Closed)
                return;
            SetTurretLightOnOff(!block.IsWorking);
        }

        // OnOff Switch Methods
        private bool IsTurretLightOnOff()
        {
            return m_turret.Enabled == true;
        }
        private void SetTurretLightOnOff(bool OnOff)
        {
            if (m_light_emmitter != null)
            {
                m_light_emmitter.ReflectorOn = !OnOff;
                m_light_emmitter.UpdateLight();
            }
          
            if (OnOff)
            {
                // turn off
                SetTurretLightColor(Light_Color);
            }
            else
            {
                // turn on
                SetTurretLightColor(Light_Color);
            }
        }

        // server calls
        private void SetServerTurretLightColor(Color color)
        {
            PlayerInput.Instance.SetColor(m_turret.EntityId, color);
        }
        private void SetServerTurretLightIntensity(float intensity)
        {
            Light_Intensity = intensity;
            PlayerInput.Instance.SetIntensity(m_turret.EntityId, intensity);
        }
        private void SetServerTurretLightOffset(float offset)
        {
            Light_Offset = offset;
            PlayerInput.Instance.SetOffset(m_turret.EntityId, offset);
        }
        private void SetServerTurretLightRadius(float radius)
        {
            Light_Radius = radius;
            PlayerInput.Instance.SetRadius(m_turret.EntityId, radius);
        }

        // Setters/Getters
        public void SetTurretLightColor(Color color)
        {
            Light_Color = color;
            //Logger.Instance.LogMessage("light: " + m_light_emmitter);
            if (m_light_emmitter != null)
            {
                m_light_emmitter.ReflectorColor = Light_Color;
                m_light_emmitter.UpdateLight();
            }
            m_light_model?.SetEmissiveParts(m_EmissiveName, Light_Color, m_turret.IsWorking ? 1 : 0);
            PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
        }
        public void SetTurretLightIntensity(float intensity)
        {
            Light_Intensity = intensity;
            if (m_light_emmitter != null)
            {
                m_light_emmitter.ReflectorIntensity = Light_Intensity;
                m_light_emmitter.UpdateLight();
            }
            PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
        }
        public void SetTurretLightRadius(float radius)
        {
           Light_Radius = radius;
            if (m_light_emmitter != null)
            {
               m_light_emmitter.ReflectorRange = Light_Radius;
                m_light_emmitter.UpdateLight();
            }
            PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
        }
        public void SetTurretLightOffset(float offset)
        {
            Logger.Instance.LogMessage("turret: " + m_turret.EntityId);
            Logger.Instance.LogMessage("light model: " + m_light_model.EntityId);
            Logger.Instance.LogMessage("light: " + m_light_emmitter.RenderObjectID);
            Light_Offset = offset;
            if (m_light_emmitter != null)
            {
               m_light_emmitter.Position = m_light_model.PositionComp.GetPosition() + m_light_model.PositionComp.GetOrientation().Forward * Light_Offset;
                m_light_emmitter.UpdateLight();
            }
            PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
        }
        public void SetTurretLightBlinkInterval(float blink_interval)
        {

            Light_Blink_Interval = blink_interval;

            //m_light.BlinkIntervalSeconds = Light_Blink_Interval;
            PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
        }
        public void SetTurretLightBlinkLength(float blink_length)
        {
            Light_Blink_Length = blink_length;

            //m_light.BlinkLength = Light_Blink_Length;
            PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
        }
        public void SetTurretLightBlinkOffset(float blink_offset)
        {
            Light_Blink_Offset = blink_offset;

            //m_light.BlinkOffset = Light_Blink_Offset;
            PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
        }
        private void GetTurretOrientation()
        {
            Turret_Azimuth = (m_turret).Azimuth;
            Turret_Elevation = (m_turret).Elevation;

            PlayerInput.Instance.SaveTerminalValues(m_turret.EntityId);
        }
        private void SetTurretOrientation(float Azimuth, float Elevation)
        {
            bool targeting = (m_turret.GetValue<bool>("TargetMeteors") || m_turret.GetValue<bool>("TargetMissiles") || m_turret.GetValue<bool>("TargetSmallShips") || m_turret.GetValue<bool>("TargetLargeShips") || m_turret.GetValue<bool>("TargetCharacters") || m_turret.GetValue<bool>("TargetStations") || m_turret.GetValue<bool>("TargetNeutrals"));
            // if we are not targeting anything
            //if (!targeting)
            {
                m_turret.Elevation = Elevation;
                m_turret.Azimuth = Azimuth;
            }
        }
    }




    public static class Extensions
    {
        /// <summary>
        /// Creates the objectbuilders in game, and syncs it to the server and all clients.
        /// </summary>
        /// <param name="entities"></param>
        public static void CreateAndSyncEntities(this List<VRage.ObjectBuilders.MyObjectBuilder_EntityBase> entities)
        {
            MyAPIGateway.Entities.RemapObjectBuilderCollection(entities);
            entities.ForEach(item => MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(item));
            MyAPIGateway.Multiplayer.SendEntitiesCreated(entities);
        }
    }

}
