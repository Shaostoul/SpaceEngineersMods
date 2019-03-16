using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace BlueKnight.ForceField
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, new string[] {"Nanowall_Fill_Large", "Nanowall_Fill_Small"})]
    public class ForceField : MyGameLogicComponent
    {
        // constants
        private const string EmissiveSubName = "Nanowall_Opaque";
        private const string EmissiveName = "Nanowall_Opaque";
        private const string PhysicsSubpartGame = "HangarDoor_door1";
        static public List<string> Subtypes = new List<string>()
        {
            "Nanowall_Fill_Large",
            "Nanowall_Fill_Small"
        };
        private const float TransparncyAmount = .5f;
        static private List<string> Patterns = new List<string>() {
            "Blank",
            "Hexagon",
			"Lines",
            "Dots",
            "Octagon"
        };
        static private List<string> Colors = new List<string>() {
            "Black",
            "Blue",
            "Green",
            "Magenta",
            "Orange",
            "Red",
            "White",
            "Yellow"
        };
        private const string OpaqueOffPatterString = "Opaque";

        // public members
        public bool m_enabledCheck = false;
        public bool m_master_block = true;

        // private members
        private IMyUpgradeModule m_field;
        private string m_field_off_model;
        private string m_field_on_model;
        private MyEntitySubpart m_field_physics;
        private static List<long> m_cube_grids = new List<long>();
        private Vector3 m_colorMaskHsv;
        private bool m_field_enabled = true;
        private bool m_readdToArray = false;
        private bool m_init = false;
        private bool m_controls_enabled = false;
        private bool m_deconstruct_check = false;
        private bool m_construct_check = false;
        private bool m_seperated_block = false;
        private bool m_color_check = false;
        private bool m_functional = false;

        // array values
        List<long> m_field_array = new List<long>();
        private Guid GUID_Field_Array = new Guid("ECF22216-6377-49C4-90D0-6B6450E9D96C");

        // terminal values
        private static Color _field_off_color = new Color(0, 0, 100, 255);
        private Color m_field_off_color = _field_off_color;
        public Color Field_Off_Color
        {
            get { return (m_field != null ? m_field_off_color : _field_off_color); }
            set { m_field_off_color = value; }
        }
        private Guid GUID_Off_ColorR = new Guid("710A7FB7-3045-4318-BB1B-1C7B3395B383");
        private Guid GUID_Off_ColorG = new Guid("58727BE4-2A57-4187-BE1A-75BF146D1067");
        private Guid GUID_Off_ColorB = new Guid("BA7D9735-F96A-4180-8BC7-CDB2B0964047");
        private static string _field_off_color_string = "Red";
        private string m_field_off_color_string = _field_off_color_string;
        public string Field_Off_Color_String
        {
            get { return (m_field != null ? m_field_off_color_string : _field_off_color_string); }
            set { m_field_off_color_string = value; }
        }
        private Guid GUID_Off_Color_String = new Guid("934FB865-204D-4E43-818B-C06A56E48CBD");
        private static string _field_off_pattern_string = OpaqueOffPatterString;
        private string m_field_off_pattern_string = _field_off_pattern_string;
        public string Field_Off_Pattern_String
        {
            get { return (m_field != null ? m_field_off_pattern_string : _field_off_pattern_string); }
            set { m_field_off_pattern_string = value; }
        }
        private Guid GUID_Off_Pattern_String = new Guid("B7839661-4518-4019-8878-1CFCA6895141");
        private static string _field_on_color_string = "Blue";
        private string m_field_on_color_string = _field_on_color_string;
        public string Field_On_Color_String
        {
            get { return (m_field != null ? m_field_on_color_string : _field_on_color_string); }
            set { m_field_on_color_string = value; }
        }
        private Guid GUID_On_Color_String = new Guid("5563B0BA-0E91-445C-A317-082C138C3317");
        private static string _field_on_pattern_string = "Blank";
        private string m_field_on_pattern_string = _field_on_pattern_string;
        public string Field_On_Pattern_String
        {
            get { return (m_field != null ? m_field_on_pattern_string : _field_on_pattern_string); }
            set { m_field_on_pattern_string = value; }
        }
        private Guid GUID_On_Pattern_String = new Guid("16CB8705-B46E-40A7-85D4-901155FA75D3");

        // Initalization
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            m_field = (Container.Entity as IMyUpgradeModule);

            string Subtype = (m_field as IMyTerminalBlock).BlockDefinition.SubtypeId;

            m_enabledCheck = false;
            for (int i = 0; i < Subtypes.Capacity; i++)
            {
                if (Subtypes[i].Equals(Subtype))
                {
                    m_enabledCheck = true;
                    break;
                }
            }
            Logger.Instance.LogMessage("Init block: " + m_enabledCheck);
            if (m_enabledCheck == true)
            {
                if (PlayerInput.Instance.isServer)
                    Logger.Instance.LogMessage("Init Server");
                else
                    Logger.Instance.LogMessage("Init");
                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;


                // event handlers
                m_field.OnClosing += OnClosing;
            }
        }
      
        public override void UpdateAfterSimulation()
        {
            // if we are a modded block
            if (m_enabledCheck)
            {
                if (PlayerInput.Instance.isServer)
                    Logger.Instance.LogMessage("Server Update After Simulation");
                else
                    Logger.Instance.LogMessage("Update After Simulation");

                // if we have not yet finished initializing 
                if (!m_init)
                {
                    if (PlayerInput.Instance != null && (m_field as MyEntity).Subparts.Count != 0)
                    {
                        NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.NONE; 
                        // get terminal values from server
                        PlayerInput.Instance.GetTerminalValues(m_field.EntityId);
                    }
                    else
                    {
                        // try again next frame
                        Logger.Instance.LogMessage("Error Initializing. Trying again Next Frame.");

                    }
                }

                // if we are checking to see if the block was seperated from the grid
                if (m_seperated_block)
                {
                    Logger.Instance.LogMessage("Seperation Check");
                    NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.NONE;
                    m_seperated_block = false;
                    // destroy the block
                    m_field.Close();
                }
                // if we are checking to see if the block has been deconstructed
                if (m_deconstruct_check)
                {
                    Logger.Instance.LogMessage("Deconstruct Check");
                    // if the block is no longer functioning
                    // if we have subparts to set
                    Logger.Instance.LogMessage("Subparts: " + (m_field as MyEntity).Subparts.Count);
                    if ((m_field as MyEntity).Subparts.Count > 0)
                    {
                        NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.NONE;
                        m_deconstruct_check = false;
                        Logger.Instance.LogMessage("Deconstruct Changed");
                        // turn off field this block belongs too
                        SetFieldArrayOnOff(false);

                        // if this is a slave block
                        if (!m_master_block)
                        {
                            // update Master's CustomInfo
                            UpdateMasterBlockInfo();

                            // show this block on the terminal
                            m_field.ShowInTerminal = true;
                        }
                    }
                }

                // if we are checking to see if the block has been constructed
                if (m_construct_check)
                {
                    Logger.Instance.LogMessage("Construct Check");  
                    // if we have subparts to set
                    Logger.Instance.LogMessage("Subparts: " + (m_field as MyEntity).Subparts.Count);
                    if ((m_field as MyEntity).Subparts.Count > 0)
                    {
                        NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.NONE;
                        m_construct_check = false;
                        // get state Master Block is in
                        var MasterBlock = MyAPIGateway.Entities.GetEntityById(m_field_array[0]) as IMyFunctionalBlock;
                        SetForceFieldOnOff(MasterBlock.IsWorking);

                        // if this is a slave block
                        if (!m_master_block)
                        {
                            // update Master's CustomInfo
                            UpdateMasterBlockInfo();

                            // hide this block from the terminal
                            m_field.ShowInTerminal = false;
                        }
                    }
                }

                // if we are checking to see if the block has been painted
                if (m_color_check)
                {
                    Logger.Instance.LogMessage("Paint Check");
                    // if we have subparts to set
                    //Logger.Instance.LogMessage("Subparts: " + (m_field as MyEntity).Subparts.Count);
                    if ((m_field as MyEntity).Subparts.Count > 0)
                    {
                        NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.NONE;
                        m_color_check = false;
                        Logger.Instance.LogMessage("Color Changed");
                        // Change back to previous appearnace
                        SetFieldArrayOnOff(m_field.Enabled);
                    }
                }
            }
        }
        public override void UpdateAfterSimulation10()
        {
            // if we are a modded block
            if (m_enabledCheck)
            {
                if (PlayerInput.Instance.isServer)
                    Logger.Instance.LogMessage("Server Update After Simulation 10");
                else
                    Logger.Instance.LogMessage("Update After Simulation 10");

                // if we are checking to see if the block was seperated from the grid
                if (m_seperated_block)
                {
                    Logger.Instance.LogMessage("Seperation Check");
                    // check for master
                    BlockRemvoedFromGrid();
                    //m_field.CubeGrid.RazeBlock(m_field.Position);
                }
            }
        }
        public override void UpdateOnceBeforeFrame()
        {
            // if we are a modded block
            if (m_enabledCheck)
            {
                if (PlayerInput.Instance.isServer)
                    Logger.Instance.LogMessage("Server Update Before Frame");
                else
                    Logger.Instance.LogMessage("Update Before Frame");
                // if we have not yet finished initializing 
                if (!m_init)
                {
                    if (PlayerInput.Instance != null && (m_field as MyEntity).Subparts.Count != 0)
                    {
                        // get terminal values from server
                        PlayerInput.Instance.GetTerminalValues(m_field.EntityId);
                    }
                    else
                    {
                        // try again next frame
                        Logger.Instance.LogMessage("Error Initializing. Trying again Next Frame.");
                        NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;

                    }
                }
                m_field.RefreshCustomInfo();
            }
        }

        public void BlockRemainsAfterSeperation()
        {
            // finish seperation check as block still remains after grid seperation
            NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.NONE;
            m_seperated_block = false;
            // recolor the block
            m_field.GameLogic.GetAs<ForceField>().SetFieldArrayOffColor(m_field.GameLogic.GetAs<ForceField>().Field_Off_Color);
            bool added = m_cube_grids.Contains(m_field.CubeGrid.EntityId);
            if (!added)
            {
                m_cube_grids.Add(m_field.CubeGrid.EntityId);
                (m_field.CubeGrid as MyCubeGrid).OnFatBlockRemoved += OnGridBlockRemoved;
                (m_field.CubeGrid as MyCubeGrid).OnFatBlockAdded += OnFatBlockAdded;
                (m_field.CubeGrid as MyCubeGrid).OnClosing += OnGridClosing;
                
            }
        }
       
        public void SetTerminalValues(ForceFieldSetting settings)
        {
            if (!m_init)
            {
                // check if this is a new block
                bool NewBlock = (!PlayerInput.Instance.isServer && settings == null) || (PlayerInput.Instance.isServer && (m_field.Storage == null || !m_field.Storage.ContainsKey(GUID_Off_ColorR)));
                // if this is a new block
                if (NewBlock)
                {
                    Logger.Instance.LogMessage("New Block Placed");
                    // this is a newly placed block turn it off to start with
                    (m_field as IMyUpgradeModule).GetActionWithName("OnOff_Off").Apply(m_field as IMyUpgradeModule);
                }

                // if this is a client
                if (!PlayerInput.Instance.isServer)
                {
                    // set this blocked Enabled var to the passed Enabled var as a fail safe
                    m_field.Enabled = settings.Enabled;
                }


                // load stored terminal data
                if (PlayerInput.Instance.isServer)
                {
                    // load data from save
                    LoadTerminalValuesFromStorage();
                }
                else
                {
                    // load data passed from server
                    LoadTerminalValuesFromServer(m_field, settings);
                }

                CreateTerminalControls();

                m_colorMaskHsv = m_field.Render.ColorMaskHsv;
                m_functional = m_field.IsFunctional;

                // add event handlers
                m_field.AppendingCustomInfo += AppendingCustomInfo;
                m_field.EnabledChanged += OnEnabledChanged;
                m_field.OnMarkForClose += OnMarkForClose;


                // if this is a new block
                if (NewBlock)
                {
                    // Add block to an array
                    AddBlockToArray(m_field.OwnerId);
                }
                else
                {
                    // check what if this is a master or slave
                    if (GetIfMasterOrSlave())
                    {
                        // set as master
                        SetFieldAsMaster();
                    }
                    else
                    {
                        var MasterBlock = MyAPIGateway.Entities.GetEntityById(m_field_array[0]);
                        // set as slave
                        SetFieldAsSlave(MasterBlock, false);
                    }
                }
               
                // set field data
                SetForceFieldSubparts();

                if (PlayerInput.Instance.isServer)
                {
                    // add this blocks cube grid to cube grids
                    bool added = m_cube_grids.Contains(m_field.CubeGrid.EntityId);
                    if (!added)
                    {
                        m_cube_grids.Add(m_field.CubeGrid.EntityId);
                        (m_field.CubeGrid as MyCubeGrid).OnFatBlockRemoved += OnGridBlockRemoved;
                        (m_field.CubeGrid as MyCubeGrid).OnClosing += OnGridClosing;
                        (m_field.CubeGrid as MyCubeGrid).OnFatBlockAdded += OnFatBlockAdded;
                    }
                    //(m_field as MyTerminalBlock).OnCubeGridChanged
                }
                // if this is not a new block
                if (!NewBlock)
                {

                }

                // if this is the server. check for any uninitialized clients for this block
                PlayerInput.Instance.SendTerminalValues(m_field.EntityId);

                // save terminal values to storage
                SaveTerminalValuesToStorage();
                // initialization complete
                m_init = true;
            }
        }

        public ForceFieldSetting LoadTerminalValuesFromStorage()
        {
            // check if we have save data
            ForceFieldSetting settings = null;
            if (m_field.Storage != null)
            {
                settings = new ForceFieldSetting();
                // if so load that data
                if (m_field.Storage.ContainsKey(GUID_Off_ColorR) && m_field.Storage.ContainsKey(GUID_Off_ColorG) && m_field.Storage.ContainsKey(GUID_Off_ColorB))
                {
                    var r = Convert.ToInt32(m_field.Storage.GetValue(GUID_Off_ColorR));
                    var g = Convert.ToInt32(m_field.Storage.GetValue(GUID_Off_ColorG));
                    var b = Convert.ToInt32(m_field.Storage.GetValue(GUID_Off_ColorB));
                    Field_Off_Color = Color.FromNonPremultiplied(r, g, b, 255);
                    settings.OffColor = Field_Off_Color;
                }

                if (m_field.Storage.ContainsKey(GUID_Off_Color_String))
                {
                    Field_Off_Color_String = m_field.Storage.GetValue(GUID_Off_Color_String);
                    settings.OffColorString = Field_Off_Color_String;
                }
                if (m_field.Storage.ContainsKey(GUID_On_Color_String))
                {
                    Field_On_Color_String = m_field.Storage.GetValue(GUID_On_Color_String);
                    settings.OnColorString = Field_On_Color_String;
                }
                if (m_field.Storage.ContainsKey(GUID_On_Pattern_String))
                {
                    Field_On_Pattern_String = m_field.Storage.GetValue(GUID_On_Pattern_String);
                    settings.OnPatternString = Field_On_Pattern_String;
                }
                if (m_field.Storage.ContainsKey(GUID_Off_Pattern_String))
                {
                    Field_Off_Pattern_String = m_field.Storage.GetValue(GUID_Off_Pattern_String);
                    settings.OffPatternString = Field_Off_Pattern_String;
                }

                if (m_field.Storage.ContainsKey(GUID_Field_Array))
                {
                    string ArrayString = m_field.Storage.GetValue(GUID_Field_Array);
                    Logger.Instance.LogMessage("Array Found During Load: " + ArrayString);
                    while (!ArrayString.Equals(""))
                    {
                        // get next entity ID
                        string ItemString = ArrayString.Substring(0, ArrayString.IndexOf("\n"));
                        m_field_array.Add(Convert.ToInt64(ItemString));

                        // remove item from string
                        ArrayString = ArrayString.Remove(0, ArrayString.IndexOf("\n")+1);
                    }
                    
                }
            }

            return settings;
        }
        public void SaveTerminalValuesToStorage()
        {
            // do this for all clients to allow blueprints
            {
                // create a storage if we don't have one
                if (m_field.Storage == null)
                    m_field.Storage = new MyModStorageComponent();

                // save data
                m_field.Storage.Add(GUID_Off_ColorR, Field_Off_Color.R.ToString());
                m_field.Storage.Add(GUID_Off_ColorG, Field_Off_Color.G.ToString());
                m_field.Storage.Add(GUID_Off_ColorB, Field_Off_Color.B.ToString());

                m_field.Storage.Add(GUID_Off_Color_String, Field_Off_Color_String);
                m_field.Storage.Add(GUID_On_Color_String, Field_On_Color_String);
                m_field.Storage.Add(GUID_Off_Pattern_String, Field_Off_Pattern_String);
                m_field.Storage.Add(GUID_On_Pattern_String, Field_On_Pattern_String);

                string ArrayString = "";
                foreach (var EntityID in m_field_array)
                {
                    ArrayString += EntityID + "\n";
                }
                if (!ArrayString.Equals(""))
                {

                    m_field.Storage.Add(GUID_Field_Array, ArrayString);
                }
            }
            
        }
        private void LoadTerminalValuesFromServer(IMyUpgradeModule door, ForceFieldSetting settings)
        {
            // load data from passed settings
            if (settings != null)
            {

                Logger.Instance.LogDebug("Found settings for block: " + door.CustomName);
                door.GameLogic.GetAs<ForceField>().Field_Off_Color = settings.OffColor;
                door.GameLogic.GetAs<ForceField>().Field_On_Pattern_String = settings.OnPatternString;
                door.GameLogic.GetAs<ForceField>().Field_Off_Pattern_String = settings.OffPatternString;
                door.GameLogic.GetAs<ForceField>().Field_On_Color_String = settings.OnColorString;
                door.GameLogic.GetAs<ForceField>().Field_Off_Color_String = settings.OffColorString;

            }
        }

        private static bool ControlsInited = false;
        private void CreateTerminalControls()
        {

            if (ControlsInited)
                return;

            ControlsInited = true;
            try
            {
                Func<IMyTerminalBlock, bool> enabledCheck = delegate (IMyTerminalBlock b) { return (b.BlockDefinition.SubtypeId == "Nanowall_Fill_Large" || b.BlockDefinition.SubtypeId == "Nanowall_Fill_Small"); };

                Func<IMyTerminalBlock, bool> enabledSliderCheck = delegate (IMyTerminalBlock b) { return (enabledCheck(b) && b.GameLogic.GetAs<ForceField>().Field_Off_Pattern_String.Equals(OpaqueOffPatterString)); };

                Func<IMyTerminalBlock, bool> enabledListCheck = delegate (IMyTerminalBlock b) { return (enabledCheck(b) && !b.GameLogic.GetAs<ForceField>().Field_Off_Pattern_String.Equals(OpaqueOffPatterString)); };

                MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalControls_CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter -= TerminalControls_CustomActionGetter;

                MyAPIGateway.TerminalControls.CustomControlGetter += TerminalControls_CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter += TerminalControls_CustomActionGetter;

                // Separator
                var sep = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyUpgradeModule>(string.Empty);
                 sep.Visible = enabledCheck;
                 MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(sep);

                // Emergency Remove From Field Array Button
                /*var RemoveFromArrayButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyUpgradeModule>("BlueKnight.ForceField.RemoveFromFieldArrayButton");

                RemoveFromArrayButton.Title = MyStringId.GetOrCompute("Remove From Field");
                RemoveFromArrayButton.Enabled = enabledCheck;
                RemoveFromArrayButton.Visible = enabledCheck;
                RemoveFromArrayButton.SupportsMultipleBlocks = true;
                RemoveFromArrayButton.Tooltip = MyStringId.GetOrCompute("ONLY USE THS if you can't find/fix ths block.");
                RemoveFromArrayButton.Action = OnRemoveFromArrayButtonClick;

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(RemoveFromArrayButton);*/

                // Appearance
                var ActiveLabel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyUpgradeModule>("BlueKnight.ForceField.ActiveLabel");
                var InactiveLabel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyUpgradeModule>("BlueKnight.ForceField.InactiveLabel");
                var fieldOffColor = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyUpgradeModule>("BlueKnight.ForceField.FieldOffColor");
                var fieldOffColorList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyUpgradeModule>("BlueKnight.ForceField.FieldOffColorList");
                var fieldOnColorList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyUpgradeModule>("BlueKnight.ForceField.FieldOnColorList");
                var fieldOffPatternList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyUpgradeModule>("BlueKnight.ForceField.FieldOffPatternList");
                var fieldOnPatternList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyUpgradeModule>("BlueKnight.ForceField.FieldOnPatternList");


                // color label
                ActiveLabel.Label = MyStringId.GetOrCompute("Active Appearance");
                ActiveLabel.Visible = enabledCheck;
                ActiveLabel.Enabled = enabledCheck;
                ActiveLabel.SupportsMultipleBlocks = true;
                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(ActiveLabel);

                // Add Lists
                fieldOnPatternList.Title = MyStringId.GetOrCompute("Pattern");
                fieldOnPatternList.Enabled = enabledCheck;
                fieldOnPatternList.Visible = enabledCheck;
                fieldOnPatternList.VisibleRowsCount = Patterns.Count;
                fieldOnPatternList.SupportsMultipleBlocks = true;
                fieldOnPatternList.Multiselect = false;
                fieldOnPatternList.ItemSelected = (b, selected) => {
                    b.GameLogic.GetAs<ForceField>().Field_On_Pattern_String = selected[0].UserData as string;
                    b.GameLogic.GetAs<ForceField>().ChangeAppearance();
                };
                fieldOnPatternList.ListContent = (b, items, selected) => {
                    if (enabledCheck(b))
                    {
                        foreach (var str in Patterns)
                        {
                            var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(str), MyStringId.NullOrEmpty, str);
                            items.Add(item);

                            if(item.UserData.Equals(b.GameLogic.GetAs<ForceField>().Field_On_Pattern_String))
                            {
                                selected.Add(item);
                            }
                        }
                    }
                };

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(fieldOnPatternList);

                fieldOnColorList.Title = MyStringId.GetOrCompute("Color");
                fieldOnColorList.Enabled = enabledCheck;
                fieldOnColorList.Visible = enabledCheck;
                fieldOnColorList.VisibleRowsCount = Colors.Count;
                fieldOnColorList.SupportsMultipleBlocks = true;
                fieldOnColorList.Multiselect = false;
                fieldOnColorList.ItemSelected = (b, selected) => {
                    b.GameLogic.GetAs<ForceField>().Field_On_Color_String = selected[0].UserData as string;
                    b.GameLogic.GetAs<ForceField>().ChangeAppearance();
                };
                fieldOnColorList.ListContent = (b, items, selected) => {
                    if (enabledCheck(b))
                    {
                        foreach (var str in Colors)
                        {
                            var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(str), MyStringId.NullOrEmpty, str);
                            items.Add(item);

                            if (item.UserData.Equals(b.GameLogic.GetAs<ForceField>().Field_On_Color_String))
                            {
                                selected.Add(item);
                            }
                        }
                    }
                };

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(fieldOnColorList);


                // color label
                InactiveLabel.Label = MyStringId.GetOrCompute("Inactive Appearance");
                InactiveLabel.Visible = enabledCheck;
                InactiveLabel.Enabled = enabledCheck;
                InactiveLabel.SupportsMultipleBlocks = true;
                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(InactiveLabel);

                // Add Lists
                fieldOffPatternList.Title = MyStringId.GetOrCompute("Pattern");
                fieldOffPatternList.Enabled = enabledCheck;
                fieldOffPatternList.Visible = enabledCheck;
                fieldOffPatternList.VisibleRowsCount = Patterns.Count+1;
                fieldOffPatternList.SupportsMultipleBlocks = true;
                fieldOffPatternList.Multiselect = false;
                fieldOffPatternList.ItemSelected = (b, selected) => {
                    b.GameLogic.GetAs<ForceField>().Field_Off_Pattern_String = selected[0].UserData as string;
                    b.GameLogic.GetAs<ForceField>().ChangeAppearance();
                    fieldOffColorList.UpdateVisual();
                    fieldOffColor.UpdateVisual();
                };
                fieldOffPatternList.ListContent = (b, items, selected) => {
                    if (enabledCheck(b))
                    {
                        var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("Opaque"), MyStringId.NullOrEmpty, OpaqueOffPatterString);
                        items.Add(item);

                        if (item.UserData.Equals(b.GameLogic.GetAs<ForceField>().Field_Off_Pattern_String))
                        {
                            selected.Add(item);
                        }

                        foreach (var str in Patterns)
                        {
                            item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(str), MyStringId.NullOrEmpty, str);
                            items.Add(item);

                            if (item.UserData.Equals(b.GameLogic.GetAs<ForceField>().Field_Off_Pattern_String))
                            {
                                selected.Add(item);
                            }
                        }
                    }
                };
                
                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(fieldOffPatternList);

                fieldOffColorList.Title = MyStringId.GetOrCompute("Color");
                fieldOffColorList.Enabled = enabledListCheck;
                fieldOffColorList.Visible = enabledCheck;
                fieldOffColorList.VisibleRowsCount = Colors.Count;
                fieldOffColorList.SupportsMultipleBlocks = true;
                fieldOffColorList.Multiselect = false;
                fieldOffColorList.ItemSelected = (b, selected) => {
                    b.GameLogic.GetAs<ForceField>().Field_Off_Color_String = selected[0].UserData as string;
                    b.GameLogic.GetAs<ForceField>().ChangeAppearance();
                };
                fieldOffColorList.ListContent = (b, items, selected) => {
                    if (enabledListCheck(b))
                    {
                        foreach (var str in Colors)
                        {
                            var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(str), MyStringId.NullOrEmpty, str);
                            items.Add(item);

                            if (item.UserData.Equals(b.GameLogic.GetAs<ForceField>().Field_Off_Color_String))
                            {
                                selected.Add(item);
                            }
                        }
                    }
                };

                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(fieldOffColorList);


                // Add color sliders
                fieldOffColor.Title = MyStringId.GetOrCompute("Color");
                fieldOffColor.Enabled = enabledSliderCheck;
                fieldOffColor.Visible = enabledCheck;
                fieldOffColor.SupportsMultipleBlocks = true;
                fieldOffColor.Setter = (b, v) =>
                {
                    if (!enabledCheck(b)) return;
                    b.GameLogic.GetAs<ForceField>().SetFieldArrayOffColor(v);
                };
                fieldOffColor.Getter = (b) => enabledCheck(b) ? b.GameLogic.GetAs<ForceField>().Field_Off_Color : Color.Black;
                MyAPIGateway.TerminalControls.AddControl<IMyUpgradeModule>(fieldOffColor);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage("Create Controls Error");
                Logger.Instance.LogMessage(ex.Message);
                Logger.Instance.LogMessage(ex.StackTrace);
            }
           
        }

        private static void TerminalControls_CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (block is IMyUpgradeModule)
            {
                string subtype = (block as IMyUpgradeModule).BlockDefinition.SubtypeId;
                var itemsToRemove = new List<IMyTerminalAction>();
                if (subtype.Equals("Nanowall_Fill_Large") || subtype.Equals("Nanowall_Fill_Small"))
                {
                    foreach (var action in actions)
                    {
                        //Logger.Instance.LogMessage("Action: " + action.Id);
                        // check if we are the master block
                        if (block.GameLogic.GetAs<ForceField>().m_master_block)
                        {
                            if (action.Id.StartsWith("OnOff"))
                            { }
                            else
                            { itemsToRemove.Add(action); }
                        }
                        else
                            itemsToRemove.Add(action);
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
            if (block is IMyUpgradeModule)
            {
                string subtype = (block as IMyUpgradeModule).BlockDefinition.SubtypeId;
                var itemsToRemove = new List<IMyTerminalControl>();
                int separatorsToKeep = 4;
                if (subtype.Equals("Nanowall_Fill_Large") || subtype.Equals("Nanowall_Fill_Small"))
                {
                    foreach (var control in controls)
                    {
                        //Logger.Instance.LogMessage("Control: " + control.Id);

                        // check if we are the master block
                        if (block.GameLogic.GetAs<ForceField>().m_master_block)
                        {

                            switch (control.Id)
                            {
                                case "OnOff":
                                case "ShowInTerminal":
                                case "ShowInToolbarConfig":
                                case "Name":
                                case "ShowOnHUD":
                                case "CustomData":
                                case "Label":
                                    break;
                                default:
                                    if (control.Id.StartsWith("BlueKnight.ForceField") && !control.Id.Equals("BlueKnight.ForceField.RemoveFromFieldArrayButton"))
                                        break;
                                    if (control is IMyTerminalControlSeparator && separatorsToKeep-- > 0)
                                        break;
                                    itemsToRemove.Add(control);
                                    break;
                            }
                        }
                        else
                        {
                            // we are a slave block we have no control
                            // unless we are damaged, then we have some control to cry out for help 
                            if (!(block as IMyFunctionalBlock).IsFunctional)
                            {
                                switch (control.Id)
                                {
                                    case "ShowOnHUD":
                                        break;
                                    default:
                                        if (control.Id.Equals("BlueKnight.ForceField.RemoveFromFieldArrayButton"))
                                            break;
                                        itemsToRemove.Add(control);
                                        break;
                                }
                            }
                            else
                            {
                                // not damaged so we have no control
                                itemsToRemove.Add(control);
                            }
                            
                        }
                    }
                }

                foreach (var action in itemsToRemove)
                {
                    controls.Remove(action);
                }
            }
        }

        // FieldArray Helpers
        public void SetFieldAsMaster()
        {
            if (!m_master_block)
            {

                // register to all event callbacks
                m_field.EnabledChanged += OnEnabledChanged;


                // show in the terminal
                m_field.ShowInTerminal = true;
                m_field.ShowInToolbarConfig = true;
                // enable all actions and terminal controls
                m_master_block = true;
            }
        }
        public void SetFieldAsSlave(IMyEntity MasterBlock, bool newBlock = true)
        {
            // set field array to show slave
            m_field_array.Clear();
            m_field_array.Add(MasterBlock.EntityId);

            // if we are currently a master
            if (m_master_block)
            {
                // unregister from all event callbacks
                m_field.EnabledChanged -= OnEnabledChanged;
            }

            // hide from the terminal if not damaged
            m_field.ShowInTerminal = (m_field.IsFunctional ? false : true);
            m_field.ShowOnHUD = false;
            m_field.ShowInToolbarConfig = false;

            // disable all actions and all terminal controls
            m_master_block = false;

            // if this is a new set all terminal values to master's values
            if (newBlock)
            {
                SetForceFieldOffColor(MasterBlock.GameLogic.GetAs<ForceField>().Field_Off_Color);
                m_field.Enabled = (MasterBlock as IMyFunctionalBlock).Enabled;

                Field_On_Pattern_String = MasterBlock.GameLogic.GetAs<ForceField>().Field_On_Pattern_String;
                Field_On_Color_String = MasterBlock.GameLogic.GetAs<ForceField>().Field_On_Color_String;
                Field_Off_Pattern_String = MasterBlock.GameLogic.GetAs<ForceField>().Field_Off_Pattern_String;
                Field_Off_Color_String = MasterBlock.GameLogic.GetAs<ForceField>().Field_Off_Color_String;
                SetForceFieldSubparts();

                SaveTerminalValuesToStorage();
            }
        }
        public void SetFieldSlaveCustomData(string OffColorString, string OnColorString, string OffPatternString, string OnPatternString)
        {

            Field_On_Pattern_String = OnPatternString;
            Field_On_Color_String = OnColorString;
            Field_Off_Pattern_String = OffPatternString;
            Field_Off_Color_String = OffColorString;
            SetForceFieldSubparts();
            SaveTerminalValuesToStorage();
        }

        // FieldArray Calls
        public bool GetIfMasterOrSlave()
        {
            // check if we have an array to belong to
            if (m_field_array.Count != 0)
            {
                // return if we are the first entity in the array
                // if we are the first, then we are the master
                return m_field_array[0] == m_field.EntityId;
            }
            // we have no array so we are the master by default
            return true;
        }
        public void AddBlockToArray(long playerID = 0, string ExistingArrayID = "")
        {
            long OwnerID;

            if (playerID == 0)
            {
                OwnerID = m_field.OwnerId;
                //Logger.Instance.LogMessage("Owner ID: " + OwnerID);
            }
            else
            {
                OwnerID = playerID;
            }


            // check if we are adding this block to an existing array
            // if this is a new block
            if (ExistingArrayID.Equals(""))
            {
                // check if there is a selected master
                string SelectedMasterID = PlayerInput.Instance.GetSelectedMaster(OwnerID);
                ExistingArrayID = SelectedMasterID;
            }

            IMyEntity MasterBlock = null;
            // if we have an exsisting ID
            if (!ExistingArrayID.Equals(""))
            {
                // get block from Exisiting ID
                MasterBlock = MyAPIGateway.Entities.GetEntityById(Convert.ToInt64(ExistingArrayID));

                // check if the Master block's grid and the passed block grid are not the same
                if (m_field.CubeGrid.EntityId != (MasterBlock as IMyTerminalBlock).CubeGrid.EntityId)
                {
                    Logger.Instance.LogMessage("Master: " + ExistingArrayID + " is on a different Grid. Adding block to new array");
                    // blocks are not on the same grid. Make this passed block a new master
                    ExistingArrayID = "";
                }
            }
            Logger.Instance.LogMessage("Add: " + m_field.EntityId + " To: " + (ExistingArrayID.Equals("") ? "Nothing" : ExistingArrayID) + " PlayerID: " + OwnerID);

            // if we are adding this block as a slave
            if (!ExistingArrayID.Equals(""))
            {
                Logger.Instance.LogMessage("Exisiting Master: " + ExistingArrayID);

                // add new block to existing array
                Logger.Instance.LogMessage("Adding Block: " + m_field.EntityId + " to Array: " + ExistingArrayID);
                MasterBlock.GameLogic.GetAs<ForceField>().m_field_array.Add(m_field.EntityId);
                MasterBlock.GameLogic.GetAs<ForceField>().SaveTerminalValuesToStorage();

                // get master of array and set block to slave
                SetFieldAsSlave(MasterBlock);
            }
            else
            {
                // no existing arrays found, make a new one for block
                m_field_array.Clear();
                m_field_array.Add(m_field.EntityId);
                SaveTerminalValuesToStorage();

                // Set new block as master
                SetFieldAsMaster();

                // Set new block a SelectedMaster
                PlayerInput.Instance.SetSelctedMaster(m_field.EntityId.ToString(), OwnerID, true);

            }
        }
        public void RemoveBlockFromArray()
        {
            try
            {
                Logger.Instance.LogMessage("Removing" + ((GetIfMasterOrSlave()) ? " Master" : "") + " Block: " + m_field.EntityId + " From Array: " + m_field_array[0]);


                // if this block was the master
                if (GetIfMasterOrSlave())
                {
                    // remove this block from the array
                    m_field_array.Remove(m_field.EntityId);
                    Logger.Instance.LogMessage("Blocks left: " + m_field_array.Count);

                    // check if the array is empty
                    if (m_field_array.Count == 0)
                    {
                        Logger.Instance.LogMessage("Removing array");

                        // only if we are the server
                        if (PlayerInput.Instance.isServer)
                        {
                            // if this block was the Selected Master
                            foreach (var master in PlayerInput.Instance.GetSelectedMasters().Keys.ToList())
                            {
                                if (PlayerInput.Instance.GetSelectedMasters()[master].Equals(m_field.EntityId.ToString()))
                                {
                                    // reset Selected Master
                                    PlayerInput.Instance.SetSelctedMaster("", master, false);
                                }
                            }
                        }
                        return;
                    }

                    // mark a new block as master
                    var MasterBlock = MyAPIGateway.Entities.GetEntityById(m_field_array[0]);
                    Logger.Instance.LogMessage("Renaming Array From: " + m_field.EntityId + " To: " + MasterBlock.EntityId);

                    // set newly marked master
                    MasterBlock.GameLogic.GetAs<ForceField>().m_field_array.Clear();
                    MasterBlock.GameLogic.GetAs<ForceField>().m_field_array = m_field_array.ToList();
                    MasterBlock.GameLogic.GetAs<ForceField>().SetFieldAsMaster();
                    MasterBlock.GameLogic.GetAs<ForceField>().SaveTerminalValuesToStorage();

                    // clear old master's array
                    m_field_array.Clear();


                    // only if we are the server
                    if (PlayerInput.Instance.isServer)
                    {
                        // if this block was the Selected Master
                        foreach (var master in PlayerInput.Instance.GetSelectedMasters().Keys.ToList())
                        {
                            if (PlayerInput.Instance.GetSelectedMasters()[master].Equals(m_field.EntityId.ToString()))
                            {
                                // reset Selected Master
                                PlayerInput.Instance.SetSelctedMaster(MasterBlock.EntityId.ToString(), master, false);
                            }
                        }
                    }
                        
                }
                else
                {
                    // remove this block from the master array
                    var MasterBlock = MyAPIGateway.Entities.GetEntityById(m_field_array[0]).GameLogic.GetAs<ForceField>();
                    MasterBlock.m_field_array.Remove(m_field.EntityId);
                    MasterBlock.SaveTerminalValuesToStorage();

                    Logger.Instance.LogMessage("Blocks left: " + MasterBlock.m_field_array.Count);
                }
            }
            catch (Exception e)
            {
                Logger.Instance.LogException(e);
            }
        }
        public void BlockRemvoedFromGrid()
        {
            // get master block from Enity ID
            var MasterBlock = MyAPIGateway.Entities.GetEntityById(m_field_array[0]).GameLogic.GetAs<ForceField>();

           
            // foreach block in the array
            foreach (var entityID in MasterBlock.m_field_array)
            {
                // get block from entry entity ID
                var PassedBlock = MyAPIGateway.Entities.GetEntityById(entityID);
                // check if the Master block's grid and the passed block grid are the same
                if ((PassedBlock as IMyTerminalBlock).CubeGrid.EntityId == (MasterBlock as IMyTerminalBlock).CubeGrid.EntityId)
                {
                    Logger.Instance.LogMessage("Block on grid with master");
                    // master is on the same grid. Do nothing to block. Pass operation back to block to finish seperation check
                    PassedBlock.GameLogic.GetAs<ForceField>().BlockRemainsAfterSeperation();

                }
                else
                {
                    Logger.Instance.LogMessage("Block not on grid with master");
                    // not on the same grid.. destory the block
                    (PassedBlock as IMyTerminalBlock).CubeGrid.RazeBlock((PassedBlock as IMyTerminalBlock).Position);
                }
            }
        }
        public int BlockArrayCount()
        {
            int count = 0;

            // get the array this block is from
            if (GetIfMasterOrSlave())
            {
                // we are the master count our array
                count = m_field_array.Count;
            }
            else
            {
                // get master array
                var MasterBlock = MyAPIGateway.Entities.GetEntityById(m_field_array[0]).GameLogic.GetAs<ForceField>();
                count = MasterBlock.m_field_array.Count;
            }

            
            return count;
        }
        public string GetBlockMaster()
        {
            // get the master of this block
            return m_field_array[0].ToString();
        }
        public bool IsArrayFunctional()
        {
            
            // check each block in the array
            foreach (var EntityID in m_field_array)
            {
                // get the current block entity
                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                
                // if the current block is not functional
                if (!(block as IMyFunctionalBlock).IsFunctional)
                {
                    // return false
                    return false;
                }
            }
            // all blocks are functional return true
            return true;
        }
        public bool IsArrayFunctional(out List<string> DamagedBlockNames)
        {
            DamagedBlockNames = new List<string>();
            bool Functional = true;
        
            // check each block in the array
            foreach (var EntityID in m_field_array)
            {
                // get the current block entity
                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                // if the current block is not functional
                if (!(block as IMyFunctionalBlock).IsFunctional)
                {
                    // set Functional to false
                    Functional = false;

                    // add block's custom name to DamageBlocksNames
                    DamagedBlockNames.Add((block as IMyTerminalBlock).CustomName);

                }
            }
            return Functional;
        }
        public void UpdateMasterBlockInfo()
        {
            // update Master's CustomInfo
            (MyAPIGateway.Entities.GetEntityById(m_field_array[0]) as IMyTerminalBlock).RefreshCustomInfo();
        }
        private void SetAllFieldOnOff(bool OnOff)
        {
            // step through Array
            foreach (var EntityID in m_field_array)
            {
                // set block enabled state
                (MyAPIGateway.Entities.GetEntityById(EntityID) as IMyFunctionalBlock).Enabled = OnOff;
                MyAPIGateway.Entities.GetEntityById(EntityID).GameLogic.GetAs<ForceField>().SetForceFieldOnOff(OnOff);
            }
        }
        public void SetAllFieldOffColor(Color color)
        {
            // get master array
            var MasterBlock = MyAPIGateway.Entities.GetEntityById(m_field_array[0]).GameLogic.GetAs<ForceField>();
            // step through Array
            foreach (var EntityID in MasterBlock.m_field_array)
            {
                // set block off color
                MyAPIGateway.Entities.GetEntityById(EntityID).GameLogic.GetAs<ForceField>().SetForceFieldOffColor(color);

                // Send to all clients to change this block's off color
                PlayerInput.Instance.SendToAllBlockOffColor(EntityID, color);
            }
        }
        public void SetFieldArrayCustomData(string OffColorString, string OnColorString, string OffPatternString, string OnPatternString)
        {
            // get master array
            var MasterBlock = MyAPIGateway.Entities.GetEntityById(m_field_array[0]).GameLogic.GetAs<ForceField>();
            // step through Array
            foreach (var EntityID in MasterBlock.m_field_array)
            {
                // set block appearence
                MyAPIGateway.Entities.GetEntityById(EntityID).GameLogic.GetAs<ForceField>().SetFieldSlaveCustomData(OffColorString, OnColorString, OffPatternString, OnPatternString);


                // Send to all clients to change this block's appearence
                PlayerInput.Instance.SendToAllBlockAppearance(EntityID, OffColorString, OnColorString, OffPatternString, OnPatternString);
            }
        }

        private void SetFieldArrayOnOff(bool OnOff)
        {
            // if the array is not functional
            if (!IsArrayFunctional())
            {
                // we are not functional
                // send "turn off" request to FieldArrays for processing
                SetAllFieldOnOff(false);
                 m_field.GetActionWithName("OnOff_Off").Apply(m_field);
               
            }
            else
            {
                // we are functional
                // send request to FieldArrays for processing
                SetAllFieldOnOff(OnOff);
            }
        }
        private void SetFieldArrayOffColor(Color color)
        {
            PlayerInput.Instance.SetOffColor(m_field.EntityId, color);
        }
        private void SetFieldArrayCustomData()
        {
            PlayerInput.Instance.SetBlockAppearance(m_field.EntityId, m_field_off_color_string, m_field_on_color_string, m_field_off_pattern_string, m_field_on_pattern_string);
        }

        // Closing Event Callbacks 
        private void OnMarkForClose(IMyEntity obj)
        {

        }
        private void OnClosing(IMyEntity obj)
        {
            NeedsUpdate = MyEntityUpdateEnum.NONE;
            (obj as IMyUpgradeModule).OnClosing -= OnClosing;
            (obj as IMyUpgradeModule).AppendingCustomInfo -= AppendingCustomInfo;

            (obj as IMyUpgradeModule).EnabledChanged -= OnEnabledChanged;
            (obj as IMyUpgradeModule).OnMarkForClose -= OnMarkForClose;

            // Remove block to an array
            RemoveBlockFromArray();
        }
        private void OnSubpartClosing(MyEntity obj)
        {
            Logger.Instance.LogMessage("Subpart Closing");
           
            (obj as MyEntitySubpart).OnClosing -= OnClosing;
            Logger.Instance.LogMessage("Subpart Closing Check.. Functional: " + m_field.IsFunctional);
            //Logger.Instance.LogMessage("Subpart Closing Check.. Model: " + m_field.Model.AssetName);

            // if we are using the defualt model
            if (m_field.Model.AssetName.Equals(MissionComponent.m_modelsFolder + "Nanowall\\Nanowall_Fill_Large.mwm"))
            {
                // redraw the block next frame
                m_color_check = true;
                // update again to readd a subpart
                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
                return;
            }


            if (!m_field.IsFunctional)
            {
                m_functional = false;
                // check next frame if the block as been decontructed
                m_deconstruct_check = true;
                // update again to readd a subpart
                NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
            }
            else
            {
                // if we were not functional before
                if (!m_functional)
                {
                    m_functional = true;
                    // check next frame if the block as been contructed
                    m_construct_check = true;
                    // update again to readd a subpart
                    NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
                }
                else
                {
                    // check if we our painting has been changed
                    if (m_field.Render.ColorMaskHsv != m_colorMaskHsv)
                    {
                        m_colorMaskHsv = m_field.Render.ColorMaskHsv;
                        // redraw the block next frame
                        m_color_check = true;
                        // update again to readd a subpart
                        NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_FRAME;
                    }
                }
            }
            

            //MyAPIGateway.Utilities.ShowNotification("subpart closing");
        }
        private void OnGridClosing(MyEntity obj)
        {
            Logger.Instance.LogMessage("Grid Closing: " + obj.EntityId);
            // remove from grids array
            /*if (m_cube_grids.Contains(obj.EntityId))
            {
                m_cube_grids.Remove(obj.EntityId);
            } */
            (obj as IMyCubeGrid).OnClosing -= OnClosing;
            (obj as MyCubeGrid).OnFatBlockRemoved -= OnGridBlockRemoved;
            (obj as MyCubeGrid).OnFatBlockAdded -= OnFatBlockAdded;

        }
        private void OnFatBlockAdded(IMyCubeBlock block)
        {
            //Logger.Instance.LogMessage("Block is ForceField: " + (block.GameLogic is ForceField));
            // check if this block is a block we want to look at
            if (block.GameLogic is ForceField)
            {
                // check if this block is a master block
                //if (block.GameLogic.GetAs<ForceField>().m_master_block)
                {
                    // check if this block is already initialized
                    if (block.GameLogic.GetAs<ForceField>().m_init)
                    {
                        // this is a Force Field block that was removed from the grid still in one peice
                        Logger.Instance.LogMessage("Block: " + block.EntityId + " Added To Grid: " + block.CubeGrid.EntityId);
                        
                        // recolor the block
                        m_field.GameLogic.GetAs<ForceField>().SetFieldArrayOffColor(m_field.GameLogic.GetAs<ForceField>().Field_Off_Color);
                        bool added = m_cube_grids.Contains(m_field.CubeGrid.EntityId);
                        if (!added)
                        {
                            m_cube_grids.Add(m_field.CubeGrid.EntityId);
                            (m_field.CubeGrid as MyCubeGrid).OnFatBlockRemoved += OnGridBlockRemoved;
                            (m_field.CubeGrid as MyCubeGrid).OnFatBlockAdded += OnFatBlockAdded;
                            (m_field.CubeGrid as MyCubeGrid).OnClosing += OnGridClosing;

                        }
                    }
                }
            }
        }
        private void OnGridBlockRemoved(IMyCubeBlock block)
        {
            //Logger.Instance.LogMessage("Block is ForceField: " + (block.GameLogic is ForceField));
            // check if this block is a block we want to look at
            if (block.GameLogic is ForceField)
            {
                // check if this block is a master block
                //if (block.GameLogic.GetAs<ForceField>().m_master_block)
                {
                    // check if this block is not already closed
                    if (!block.MarkedForClose)
                    {
                        // this is a Force Field block that was removed from the grid still in one peice
                        Logger.Instance.LogMessage("Block: " + block.EntityId + " Removed From Grid: " + block.CubeGrid.EntityId);
                        // get the grid this block came from
                        foreach (var entityId in m_cube_grids)
                        {
                            if (entityId == block.CubeGrid.EntityId)
                            {
                                // mark this block as seperated
                                //grid.RazeBlock(block.Position);
                                block.GameLogic.GetAs<ForceField>().m_seperated_block = true;
                                block.GameLogic.GetAs<ForceField>().NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.EACH_10TH_FRAME;
                                break;
                            }
                        }
                    } 
                }
            }
        }
        private void OnIsWorkingChanged(IMyCubeBlock block)
        {
            Logger.Instance.LogMessage("On Is Working Changed");
        }

        // Change Event Callbacks 
        private void OnEnabledChanged(IMyTerminalBlock block)
        {
            if (block == null || block.Closed)
                return;
            // change field state
            m_field_enabled = (block as IMyFunctionalBlock).Enabled;
            Logger.Instance.LogMessage("Enabled Changed");
            SetFieldArrayOnOff(m_field_enabled);
        }
        private static void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder StrBuilder)
        {
            try
            {
                var door = (block as IMyTerminalBlock).GameLogic.GetAs<ForceField>();

                StringBuilder text = new StringBuilder(100);
               
                text.Append("Nanowall: ");
                text.Append((door.IsForceFieldOnOff() ? "Active" : "Inactive") + "\n");

                // if this is a master block
                if (door.m_master_block)
                {
                    // if we are disabled
                    if (!door.IsForceFieldOnOff())
                    {
                        List<string> BrokenNames = new List<string>();

                        // check if the array is not functional
                        if (!door.IsArrayFunctional(out BrokenNames))
                        {
                            // output we have a broken Field with the broken block names
                            text.Append("\nBroken Field.\nBlocks in need of Repair:\n");
                            foreach(var name in BrokenNames)
                            {
                                text.Append(name + "\n");
                            }
                        }
                    }
                }

                StrBuilder.Append(text);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogException(ex);
            }
        }
        private void ChangeAppearance()
        {
            // change custom values
            SetFieldArrayCustomData();
        }

        


        // OnOff Switch Methods
        private bool IsForceFieldOnOff()
        {
            return m_field.Enabled == true;
        }
        public void SetForceFieldOnOff(bool OnOff)
        {
            // check if block is not functioning
            Logger.Instance.LogMessage("On Off Check.. Functional: " + (m_field.IsFunctional));
            if (!m_field.IsFunctional)
            {
                // disable part
                if (m_field_physics == null || m_field_physics.Closed)
                {
                    // enable physics for the off subpart
                    m_field_physics = null;
                    Logger.Instance.LogMessage("subparts:" + (m_field as MyEntity).Subparts.Count);
                    foreach (var p in (m_field as MyEntity).Subparts)
                    {
                        Logger.Instance.LogMessage("Part: " + p.Key);
                        Logger.Instance.LogMessage("Part 2: " + p.Value);
                    }
                    m_field.TryGetSubpart(PhysicsSubpartGame, out m_field_physics);
                    m_field_physics.OnClosing -= OnSubpartClosing;
                    m_field_physics.OnClosing += OnSubpartClosing;
                    Logger.Instance.LogMessage("deconstruct physics: " + (m_field_physics.Physics == null));
                    if (m_field_physics.Physics == null)
                    {

                        // init physics
                        MyPhysicsHelper.InitModelPhysics(m_field_physics, RigidBodyFlag.RBF_KINEMATIC, CollisionLayers.CharacterCollisionLayer);
                    }
                }
             
            }
            // if we are functioning
            else
            {
                if (OnOff)
                {

                    // turn on  
                    if (!PlayerInput.Instance.isDedicated)
                    {
                        (m_field as MyEntity).Render.Visible = true;

                        (m_field as MyEntity).RefreshModels(m_field_on_model, null);
                        (m_field as MyEntity).Render.RemoveRenderObjects();
                        (m_field as MyEntity).Render.UpdateRenderObject(true);
                    }
                  
                   
                    m_field_physics = null;

                    // setup on subpart to react to being closed
                    m_field.TryGetSubpart(PhysicsSubpartGame, out m_field_physics);
                    m_field_physics.OnClosing -= OnSubpartClosing;
                    m_field_physics.OnClosing += OnSubpartClosing;
                    
                    if (m_field_physics.Physics == null)
                    {

                        // init physics
                        MyPhysicsHelper.InitModelPhysics(m_field_physics, RigidBodyFlag.RBF_KINEMATIC, CollisionLayers.CharacterCollisionLayer);
                    }
                    Logger.Instance.LogMessage("on physics: " + (m_field_physics.Physics == null));
                    // disable physics if it is enabled
                    if (m_field_physics != null && !m_field_physics.Closed)
                    {
                        if (m_field_physics.Physics != null && m_field_physics.Physics.Enabled)
                        {
                            m_field_physics.Physics.Enabled = false;
                        }
                        Logger.Instance.LogMessage("on physics enabled: " + (m_field_physics.Physics.Enabled));
                    }

                }
                else
                {
                    // turn off
                    if (!PlayerInput.Instance.isDedicated)
                    {
                        (m_field as MyEntity).Render.Visible = true;
                        (m_field as MyEntity).RefreshModels(m_field_off_model, null);
                        (m_field as MyEntity).Render.RemoveRenderObjects();
                        (m_field as MyEntity).Render.UpdateRenderObject(true);
                    }
                    SetForceFieldColor();

                    // enable physics for the off subpart
                    m_field_physics = null;

                    m_field.TryGetSubpart(PhysicsSubpartGame, out m_field_physics);
                    m_field_physics.OnClosing -= OnSubpartClosing;
                    m_field_physics.OnClosing += OnSubpartClosing;
                    Logger.Instance.LogMessage("off physics: " + (m_field_physics.Physics == null));
                    if (m_field_physics.Physics == null)
                    {

                        // init physics
                        MyPhysicsHelper.InitModelPhysics(m_field_physics, RigidBodyFlag.RBF_KINEMATIC, CollisionLayers.CharacterCollisionLayer);
                    }

                    if (m_field_physics != null && !m_field_physics.Closed)
                    {

                        if (m_field_physics.Physics != null && !m_field_physics.Physics.Enabled)
                        {
                            m_field_physics.Physics.Enabled = true;
                        }
                    }
                }
            }
            m_field.RefreshCustomInfo();
        }
    
        // Setters/Getters
        public void SetForceFieldOffColor(Color color)
        {
            Field_Off_Color = color;

            SetForceFieldColor();
            SaveTerminalValuesToStorage();
        }
        private void SetForceFieldColor()
        {
            // if we are using the "None" as our Off Pattern
            if (Field_Off_Pattern_String != null && Field_Off_Pattern_String.Equals(OpaqueOffPatterString) && m_field.IsFunctional)
            {
                m_field?.SetEmissiveParts(EmissiveSubName, Field_Off_Color, 0);
            }
        }
        private void SetForceFieldSubparts()
        {
            Logger.Instance.LogMessage("subparts:" + (m_field as MyEntity).Subparts.Count);
            foreach (var p in (m_field as MyEntity).Subparts)
            {
                Logger.Instance.LogMessage("Part: " + p.Key);
                Logger.Instance.LogMessage("Part 2: " + p.Value);
            }
            // get new models
            string size = (m_field.CubeGrid.GridSizeEnum == MyCubeSize.Small ? "_Small" : "_Large");

            // Active Model
            m_field_on_model = $"{MissionComponent.m_modelsFolder + @"Nanowall\"}{"Nanowall_" + Field_On_Pattern_String + "_" + Field_On_Color_String + size + ".mwm"}";

            // are we using an opaque or transparent off field
            if (Field_Off_Pattern_String.Equals(OpaqueOffPatterString))
            {
                // opaque
                m_field_off_model = $"{MissionComponent.m_modelsFolder + @"Nanowall\"}{"Nanowall_Opaque" + size + ".mwm"}";
            }
            else
            {
                // transparent
                m_field_off_model = $"{MissionComponent.m_modelsFolder + @"Nanowall\"}{"Nanowall_" + Field_Off_Pattern_String + "_" + Field_Off_Color_String + size + ".mwm"}";
            }
            // turn on new subpart
            SetForceFieldOnOff(m_field.Enabled);
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

        public static void RemoveAtUnordered<T>(this List<T> list, int index)
        {
            var item = list[index];

            if (list.Count < 2)
            {
                list.RemoveAt(index);
            }
            else
            {
                list[index] = list[list.Count - 1];
                list.RemoveAt(list.Count - 1);
            }
        }
    }

}
