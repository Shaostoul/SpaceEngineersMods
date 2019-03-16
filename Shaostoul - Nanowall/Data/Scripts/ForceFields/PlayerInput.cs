#if DEBUG
#define PROFILE
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Draygo.API;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Entities.Inventory;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Sandbox.ModAPI.Weapons;
using Sandbox.Game.EntityComponents;
using System.IO;
using ProtoBuf;
using System.Linq;
using System.ComponentModel;
using VRage.Utils;
using SpaceEngineers.Game.Entities.Blocks;
using Sandbox.Common.ObjectBuilders;

namespace BlueKnight.ForceField
{
    [ProtoContract]
    public class ForceFieldSetting
    {
        [ProtoMember]
        public long EntityId = 0;

        [ProtoMember]
        public Color OffColor = Color.MediumBlue;

        [ProtoMember]
        public string OffColorString = "";

        [ProtoMember]
        public string OnColorString = "";

        [ProtoMember]
        public string OffPatternString = "";

        [ProtoMember]
        public string OnPatternString = "";

        [ProtoMember]
        public bool Enabled = false;


    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    class MissionComponent : MySessionComponentBase
    {
        private const string localModName = "Shaostoul - Nanowall";
        private const string steamModName = "Nanowalls";
        public static string m_modelsFolder = "";
        public override void LoadData()
        {
            Logger.Instance.Init("ForceField");
            Logger.Instance.Debug = true;
            Logger.Instance.LogMessage("Mod Version: 1.0.0");

            // get models folder for swapping models
            // Scan for publisher ID
            ulong publishID = 0;
            var mods = MyAPIGateway.Session.Mods;
            Logger.Instance.LogMessage("mods: " + mods.Count);
            foreach (var mod in mods)
            {
                Logger.Instance.LogMessage("mod: " + mod.FriendlyName);
                if (localModName.Equals(mod.FriendlyName) || steamModName.Equals(mod.FriendlyName)) publishID = mod.PublishedFileId;
            }
            Logger.Instance.LogMessage("Mod Publish ID: " + publishID);

            // Figure out mod's directory
            if (publishID != 0)
            {
                m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\Models\", ModContext.ModPath));
            }
            else
            {
                m_modelsFolder = Path.GetFullPath(string.Format(@"{0}\{1}\Models\", MyAPIGateway.Utilities.GamePaths.ModsPath, localModName));
            }
            Logger.Instance.LogMessage("Mod Model Folder Path: " + m_modelsFolder);
        }
        protected override void UnloadData()
        {
            Logger.Instance.Close();
        }

        public override void SaveData()
        {
            if (PlayerInput.Instance.isServer)
            {
                
            }          
        }

        bool m_init = false;
        public override void UpdateBeforeSimulation()
        {
            if (!m_init)
            {
                m_init = true;


            }
        }
    }


    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class PlayerInput : MySessionComponentBase
    {
        private const ushort _RCV_FROMCLIENT = 10801;
        private const ushort _SND_REDMESSAGE = 10802;
        private const ushort _SND_WHITEMESSAGE = 10803;
        private const ushort _SND_DATATOCLIENT = 10804;

        private bool init = false;
        public bool isDedicated = false;
        public bool isServer = false;
        public bool isOnline = false;

        public static PlayerInput Instance;

        private HudAPIv2 HUDText;
        private HudAPIv2.HUDMessage HUDmsg;
        private HudAPIv2.BillBoardHUDMessage HUDbillMsg;
        private HudAPIv2.HUDMessage HUDmsgPlc;
        private HudAPIv2.BillBoardHUDMessage HUDbillMsgPlc;
        private Vector2D HUDmsgOrigin = new Vector2D(-0.95, 0.95);
        private Color HUDbillColor = Color.FromNonPremultiplied(0, 50, 50, 150);
        private float HUDmsgPadding = 0.03f;
        private string HUDmsgSelectedMasterColor = "cyan";
        private string HUDmsgCurrentMasterColor = "purple";
        private string HUDmsgSlaveColor = "green";
        private string HUDmsgTextColor = "white";

        private long HUDmsgEntityId = 0;
        private MyCubeBlockDefinition HUDmsgBlockDefinition = null;

        private string HUDmsgSelectedMaster = "";
        private string HUDmsgCurrentMaster = "";

        private Dictionary<long, string> SelectedMasterID = new Dictionary<long, string>();
        private Dictionary<ulong, Dictionary<string, ForceFieldSetting>> PreInitBlockSettings = new Dictionary<ulong, Dictionary<string, ForceFieldSetting>>();


        public bool IsInMenu
        {
            get
            {
                if (MyAPIGateway.Gui == null)
                    return false;

                return (MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible);
            }
        }

        public long PlayerId
        {
            get
            {
                if (MyAPIGateway.Session?.Player == null)
                    return 0;

                return MyAPIGateway.Session.Player.IdentityId;
            }
        }

        enum BlockAction : ushort
        {
            None = 0,
            SelectMaster,
            RemoveBlock,
            SetSlave,
            GetTerminalValues,
            SetSlaveOrMaster,
            SetOffColor,
            SetAppearance,
            GetPlacementHudInfo,
            GetHudInfo
        }

        public void Init()
        {
            if (init)
                return; //class already initialized, abort.
            Logger.Instance.LogMessage("Init Player Input");
            init = true;
            Instance = this;
            isServer = MyAPIGateway.Session.OnlineMode == MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer;
            isOnline = MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE;
            isDedicated = MyAPIGateway.Utilities.IsDedicated && isServer;

            // load Field Array data
            if (isServer)
            {
                Logger.Instance.LogMessage("Loading Server Data");
            }


            if (isServer && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
                MyAPIGateway.Multiplayer.RegisterMessageHandler(_RCV_FROMCLIENT, RecieveFromClient);

            //if (isDedicated)
            {
                HUDText = new HudAPIv2();

                MyAPIGateway.Multiplayer.RegisterMessageHandler(_SND_DATATOCLIENT, RecieveActionFromServer);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(_SND_REDMESSAGE, RecieveRedMessage);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(_SND_WHITEMESSAGE, RecieveWhiteMessage);
            }
        }

        public override void LoadData()
        {
            if (!init)
            {
                Logger.Instance.LogMessage("Player Input: init");
                if (MyAPIGateway.Session == null)
                {
                    Logger.Instance.LogMessage("Player Input: input no session");
                    return;
                }


                if (MyAPIGateway.Multiplayer == null && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
                {
                    Logger.Instance.LogMessage("Player Input: input bad multiplayer");
                    return;
                }

                Init();
            }
        }

        protected override void UnloadData()
        {
            if (isServer && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(_RCV_FROMCLIENT, RecieveFromClient);

            //if (isDedicated)
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(_SND_DATATOCLIENT, RecieveActionFromServer);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(_SND_REDMESSAGE, RecieveRedMessage);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(_SND_WHITEMESSAGE, RecieveWhiteMessage);

                if (HUDText != null)
                    HUDText.Close();
            }
        }

        public override void UpdateAfterSimulation()
        {
            /*if (HUDText != null && HUDText.Heartbeat)
            {
                if (HUDmsg == null)
                    HUDmsg = new HudAPIv2.HUDMessage(new StringBuilder(), new Vector2D(-0.95, 0.8), Scale: 1.5) { Visible = false };

                if (notifications.Count > 0)
                {
                    UpdateNotifications();
                }
                else
                {
                    HUDmsg.Visible = false;
                }
            }*/

            // if user is in menu return.
            if (IsInMenu)
                return;
            if (!isDedicated)
            {
                UpdateInputActions();
                BlockInfoHudCheck();
            }

        }
        private void BlockInfoHudCheck()
        {
            if (!(MyAPIGateway.Session?.Player?.Controller?.ControlledEntity is IMyCharacter))
                return;

            // check if player is about to place a block
            MyCubeBlockDefinition BlockDefinition = null;
            //if (MyCubeBuilder != null)
            
            BlockDefinition = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
            CheckBlockDefinition(BlockDefinition);

            if (BlockDefinition == null)
            {
                // if not check if the player is holding a tool
                var tool = MyAPIGateway.Session?.Player?.Character?.EquippedTool as IMyEngineerToolBase;

                var block = tool?.Components?.Get<MyCasterComponent>()?.HitBlock as IMySlimBlock;
                if (block == null || block.FatBlock == null)
                {
                    // not facinng a block with a tool hide tool message
                    ShowHudMessage(false);
                    return;
                }

                var blockEntity = block.FatBlock as MyEntity;

                if (blockEntity == null || !(blockEntity is IMyUpgradeModule) || !(blockEntity.GameLogic is ForceField))
                {
                    ShowHudMessage(false);
                    return;
                }

                ShowHudMessage(true, blockEntity as IMyTerminalBlock);
            }
            else
            {

                // we are not holding a tool hide info hud
                ShowHudMessage(false);
            }
        }
        private void UpdateInputActions()
        {

            if (!MyAPIGateway.Input.IsNewRightMouseReleased())
                return;

            var action = BlockAction.None;

            if (MyAPIGateway.Input.IsAnyShiftKeyPressed())
                action = BlockAction.SelectMaster;

            if (MyAPIGateway.Input.IsAnyAltKeyPressed())
            {
                action = BlockAction.RemoveBlock;
            }
            else if (MyAPIGateway.Input.IsAnyCtrlKeyPressed())
            {
                action = BlockAction.SetSlave;
            }

            if (action != BlockAction.None)
            {

                if (!(MyAPIGateway.Session.Player.Controller.ControlledEntity is IMyCharacter))
                    return;

                var tool = MyAPIGateway.Session?.Player?.Character?.EquippedTool as IMyEngineerToolBase;

                var block = tool?.Components?.Get<MyCasterComponent>()?.HitBlock as IMySlimBlock;
                if (block == null || block.FatBlock == null)
                    return;

                var blockEntity = block.FatBlock as MyEntity;

                if (blockEntity == null || !(blockEntity is IMyUpgradeModule) || !(blockEntity.GameLogic is ForceField))
                    return;


                if (!isServer)
                {
                    SendToServer(blockEntity, PlayerId, action);
                }
                else
                {
                    TryDoServerAction(blockEntity, PlayerId, 0, action, null);
                }
            }

        }

        private void CheckBlockDefinition(MyCubeBlockDefinition blockDefinition)
        {
            bool Show = false;
            // if we have a block selected
            if (blockDefinition != null)
            {

                // test if this is one of our blocks
                if (blockDefinition.Id.TypeId.ToString().Equals("MyObjectBuilder_UpgradeModule"))
                {
                    foreach (var subpart in ForceField.Subtypes)
                    {
                        if (subpart.Equals(blockDefinition.Id.SubtypeId.ToString()))
                        {
                            Show = true;
                            break;
                        }
                    }
                }
            }
            ShowHudPlacementMessage(Show, blockDefinition);
        }
        private void ShowHudMessage(bool Show, IMyTerminalBlock block = null)
        {
            if (HUDText != null && HUDText.Heartbeat)
            {
                if (HUDmsg == null)
                    HUDmsg = new HudAPIv2.HUDMessage(new StringBuilder(), HUDmsgOrigin, Scale: 1) { Visible = false};
                if (HUDbillMsg == null)
                    HUDbillMsg = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("Square"), HUDmsgOrigin, HUDbillColor, Scale: 1) { Visible = false };

                // update message
                if (Show && HUDmsgEntityId != block.EntityId)
                {
                    HUDmsgEntityId = block.EntityId;
                    GetHUDInfo(block);
                }

                // update message visibility     
               
                HUDmsg.Visible = Show;
                HUDbillMsg.Visible = Show;
            }
        }
        private void ShowHudPlacementMessage(bool Show, MyCubeBlockDefinition blockDefinition)
        {
            if (HUDText != null && HUDText.Heartbeat)
            {
                if (HUDmsgPlc == null)
                    HUDmsgPlc = new HudAPIv2.HUDMessage(new StringBuilder(), HUDmsgOrigin, Scale: 1) { Visible = false };
                if (HUDbillMsgPlc == null)
                {
                    HUDbillMsgPlc = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("Square"), HUDmsgOrigin, HUDbillColor, Scale: 1) { Visible = false };
                }


                // update message
                // if the selected block has changed
                if (Show && (HUDmsgBlockDefinition == null || blockDefinition.Id != HUDmsgBlockDefinition.Id))
                {
                    HUDmsgBlockDefinition = blockDefinition;
                    GetPlacementHUDInfo();
                }
                else
                {
                    HUDmsgBlockDefinition = null;
                }
                // update message visibility

                HUDmsgPlc.Visible = Show;
                HUDbillMsgPlc.Visible = Show;
            }
        }
        private void UpdateHudMessage(IMyTerminalBlock block, string SelectedMaster, string CurrentMaster, int NumBlocks)
        {

            if (HUDText != null && HUDText.Heartbeat)
            {

                HUDmsgSelectedMaster = SelectedMaster;
                HUDmsgCurrentMaster = CurrentMaster;

                IMyTerminalBlock SelectedMasterBlock = null;
                if (SelectedMaster != "")
                    SelectedMasterBlock = MyAPIGateway.Entities.GetEntityById(Convert.ToInt64(SelectedMaster)) as IMyTerminalBlock;
                var CurrentMasterBlock = MyAPIGateway.Entities.GetEntityById(Convert.ToInt64(CurrentMaster)) as IMyTerminalBlock;
                var text = HUDmsg.Message;
                text.Clear();

                // Select Master
                if (SelectedMasterBlock != null && CurrentMasterBlock.EntityId == SelectedMasterBlock.EntityId)
                {
                    text.AppendFormat("<color={0}>{1}<color={2}>{3}", HUDmsgCurrentMasterColor, CurrentMasterBlock.CustomName, HUDmsgTextColor, " is the Selected Master\n");
                }
                else
                {
                    text.AppendFormat("<color={0}>{1}", "yellow", "Shift + Right Click: ");
                    text.AppendFormat("<color={0}>{1}<color={2}>{3}<color={0}>{4}", HUDmsgTextColor, "Make ", HUDmsgCurrentMasterColor, CurrentMasterBlock.CustomName, " the new Selected Master\n");
                }

                var BlockColor = (block.EntityId == CurrentMasterBlock.EntityId) ? HUDmsgCurrentMasterColor : HUDmsgSlaveColor;

                // Set Slave
                if (SelectedMasterBlock != null)
                {
                    if (block.CubeGrid.EntityId != SelectedMasterBlock.CubeGrid.EntityId)
                    {
                        text.AppendFormat("<color={3}>{0}<color={1}>{2}<color={3}>{4}<color={5}>{6}<color={3}>{7}", "Cannot set as Slave to: ", HUDmsgSelectedMasterColor, SelectedMasterBlock.CustomName, "red", " not on the same grid as ", BlockColor, block.CustomName, ". Setting as Slave will remove this block from its current Group.\n");
                    }
                    else
                    {
                        if (block.EntityId == SelectedMasterBlock.EntityId)
                        {
                            text.AppendFormat("<color={0}>{1}<color={2}>{3}", BlockColor, block.CustomName, HUDmsgTextColor, " is the Selected Master\n");
                        }
                        else if (CurrentMasterBlock.EntityId == SelectedMasterBlock.EntityId)
                        {
                            text.AppendFormat("<color={0}>{1}<color={2}>{3}<color={4}>{5}", BlockColor, block.CustomName, HUDmsgTextColor, " is already a Slave of ", HUDmsgSelectedMasterColor, CurrentMasterBlock.CustomName + "\n");
                        }
                        else
                        {
                            text.AppendFormat("<color={0}>{1}", "yellow", "Control + Right Click: ");
                            text.AppendFormat("<color={0}>{1}<color={2}>{3}<color={0}>{4}<color={5}>{6}", HUDmsgTextColor, "Set ", BlockColor, block.CustomName, " as a Slave to ", HUDmsgSelectedMasterColor, SelectedMasterBlock.CustomName + "\n");
                        }
                    }
                }
                else
                {
                    text.AppendFormat("<color={0}>{1}", "red", "Cannot set as a Slave. No Master Selected.\n");
                }

                // Remove Block
                if (NumBlocks <= 1)
                {
                    text.AppendFormat("<color={3}>{1}<color={0}>{2}<color={3}>{4}", BlockColor, "Cannot Remove ", block.CustomName, "red", ". It is already by itself.\n");
                }
                else
                {
                    text.AppendFormat("<color={0}>{1}", "yellow", "Alt + Right Click: ");
                    text.AppendFormat("<color={0}>{1}<color={2}>{3}<color={0}>{4}<color={5}>{6}", HUDmsgTextColor, "Set ", BlockColor, block.CustomName, " as its own Group removing it from ", HUDmsgCurrentMasterColor, CurrentMasterBlock.CustomName + "\n");
                }
                var Height = (float)HUDmsg.GetTextLength().Y;
                var Width = (float)HUDmsg.GetTextLength().X;
                HUDbillMsg.Origin = new Vector2D(HUDmsg.Origin.X + (Width / 2), HUDmsg.Origin.Y + (Height / 2));
                HUDbillMsg.Height = Height - HUDmsgPadding;
                HUDbillMsg.Width = Width + HUDmsgPadding;
            }
           
        }
        private void UpdateHudPlacementMessage(string SelectedMaster)
        {
            if (HUDText != null && HUDText.Heartbeat)
            {
                //if (SelectedMaster.Equals("") || !HUDmsgSelectedMaster.Equals(SelectedMaster))
                {
                    HUDmsgSelectedMaster = SelectedMaster;

                    IMyTerminalBlock SelectedMasterBlock = null;
                    if (SelectedMaster != "")
                        SelectedMasterBlock = MyAPIGateway.Entities.GetEntityById(Convert.ToInt64(SelectedMaster)) as IMyTerminalBlock;

                    var text = HUDmsgPlc.Message;
                    text.Clear();

                    // Select Master
                    if (SelectedMasterBlock != null)
                    {
                        //if (block.CubeGrid.EntityId != SelectedMasterBlock.CubeGrid.EntityId)
                        if (false)
                        {
                            //text.AppendFormat("<color={3}>{0}<color={1}>{2}<color={3}>{4}<color={5}>{6}<color={3}>{7}", "Cannot set as Slave to: ", HUDmsgSelectedMasterColor, SelectedMasterBlock.CustomName, "red", " not on the same grid as ", BlockColor, block.CustomName, ". Setting as Slave will remove this block from its current Group.\n");
                        }
                        else
                        {
                            text.AppendFormat("<color={0}>{1}<color={2}>{3}", HUDmsgSelectedMasterColor, SelectedMasterBlock.CustomName, HUDmsgTextColor, " is the Selected Master\n");
                        }
                    }
                    else
                    {
                        text.AppendFormat("<color={0}>{1}", "red", "No Master Selected. Placing block will make it the new Selected Master\n");
                    }
                }
                var Height = (float)HUDmsgPlc.GetTextLength().Y;
                var Width = (float)HUDmsgPlc.GetTextLength().X;
                HUDbillMsgPlc.Origin = new Vector2D(HUDmsgPlc.Origin.X + (Width / 2), HUDmsgPlc.Origin.Y + (Height / 2));
                HUDbillMsgPlc.Height = Height - HUDmsgPadding;
                HUDbillMsgPlc.Width = Width + HUDmsgPadding;
            }
                
        }

        private void SendToServer(IMyEntity entityblock, long playerID, BlockAction action, ForceFieldSetting settings = null)
        {
            try
            {
                if (playerID == 0)
                    return;

                NetworkMessage msg = new NetworkMessage();
                if (entityblock == null)
                {
                    msg.BlockEntityId = 0;
                }
                else
                {
                    msg.BlockEntityId = entityblock.EntityId;
                }


                msg.PlayerId = playerID;
                msg.SteamId = MyAPIGateway.Multiplayer.MyId;
                msg.Action = (ushort)action;
                // compile block data
                if (settings != null)
                {
                    // send passed values
                    msg.OffColorR = settings.OffColor.R;
                    msg.OffColorG = settings.OffColor.G;
                    msg.OffColorB = settings.OffColor.B;
                    msg.OffColorA = settings.OffColor.A;
                    msg.OffColorString = settings.OffColorString;
                    msg.OnColorString = settings.OnColorString;
                    msg.OffPatternString = settings.OffPatternString;
                    msg.OnPatternString = settings.OnPatternString;
                }

                Logger.Instance.LogMessage("Send message to server: " + msg.SteamId + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);

                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                MyAPIGateway.Multiplayer.SendMessageToServer(_RCV_FROMCLIENT, msgBytes, true);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(ex.Message);
                Logger.Instance.LogMessage(ex.StackTrace);
            }

        }

        // Server Callbacks
        private void RecieveFromClient(byte[] msgBytes)
        {
            try
            {

                NetworkMessage msg = MyAPIGateway.Utilities.SerializeFromBinary<NetworkMessage>(msgBytes);

                Logger.Instance.LogMessage("Message Recieved From: " + msg.SteamId + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);

                if (!MyAPIGateway.Entities.EntityExists(msg.BlockEntityId) && ((BlockAction)msg.Action != BlockAction.GetPlacementHudInfo))
                    return;

                var blockEntity = MyAPIGateway.Entities.GetEntityById(msg.BlockEntityId) as MyEntity;

                if (blockEntity == null && ((BlockAction)msg.Action != BlockAction.GetPlacementHudInfo))
                    return;

                if (blockEntity is IMyCubeBlock || ((BlockAction)msg.Action == BlockAction.GetPlacementHudInfo))
                    TryDoServerAction(blockEntity, msg.PlayerId, msg.SteamId, (BlockAction)msg.Action, msg);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(ex.Message);
                Logger.Instance.LogMessage(ex.StackTrace);
            }

        }

        private void TryDoServerAction(MyEntity blockEntity, long playerId, ulong steamId, BlockAction action, NetworkMessage data)
        {
            if (playerId == 0)
                return;

            // check for access if this is an action that requires it
            switch (action)
            {
                case BlockAction.RemoveBlock:
                case BlockAction.SelectMaster:
                case BlockAction.SetSlave:
                    var relation = ((IMyCubeBlock)blockEntity).GetUserRelationToOwner(playerId);


                    if (relation == MyRelationsBetweenPlayerAndBlock.Owner
                        || relation == MyRelationsBetweenPlayerAndBlock.FactionShare
                        || relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                    {
                        Logger.Instance.LogMessage("Action Taken. PlayerID: " + playerId + " SteamID: " + steamId + " Action: " + action + " BlockID: " + blockEntity.EntityId);
                        PerformServerAction(blockEntity, playerId, steamId, action, data);
                    }
                    else
                    {
                        Logger.Instance.LogMessage("Access Denied. PlayerID: " + playerId + " SteamID: " + steamId + " Action: " + action + " BlockID: " + blockEntity.EntityId);
                        SendRedMessageToClient(steamId, "Access Denied.");
                    }
                    break;
                default:
                    Logger.Instance.LogMessage("Action Taken. PlayerID: " + playerId + " SteamID: " + steamId + " Action: " + action + " BlockID: " + blockEntity?.EntityId);
                    // any other action require no permissions
                    PerformServerAction(blockEntity, playerId, steamId, action, data);
                    break;
            }


        }

        private void PerformServerAction(MyEntity blockEntity, long playerId, ulong steamId, BlockAction action, NetworkMessage data)
        {
            if (action == BlockAction.SelectMaster)
            {
                DoServerSelectMaster(blockEntity, playerId, steamId);
            }
            else if (action == BlockAction.SetSlave)
            {
                DoServerSetSlave(blockEntity, playerId, steamId);
            }
            else if (action == BlockAction.RemoveBlock)
            {
                DoServerRemoveBlock(blockEntity, playerId, steamId);
            }
            else if (action == BlockAction.GetTerminalValues)
            {
                DoServerGetTerminalValues(blockEntity, steamId);
            }
            else if (action == BlockAction.SetOffColor)
            {
                Color color = Color.FromNonPremultiplied(data.OffColorR, data.OffColorG, data.OffColorB, data.OffColorA);
                DoServerSetOffColor(blockEntity, steamId, color);
            }
            else if (action == BlockAction.SetAppearance)
            {
                DoServerSetAppearance(blockEntity, steamId, data.OffColorString, data.OnColorString, data.OffPatternString, data.OnPatternString);
            }
            else if (action == BlockAction.GetPlacementHudInfo)
            {
                DoServerGetPlacementHudInfo(playerId, steamId);
            }
            else if (action == BlockAction.GetHudInfo)
            {
                DoServerGetHudInfo(blockEntity, playerId, steamId);
            }
        }

        private void DoServerGetPlacementHudInfo(long playerId, ulong steamId)
        {
            // get info
            var SelectedMaster = GetSelectedMaster(playerId);

            // send info back to client
            SendActionToClient(0, steamId, BlockAction.GetPlacementHudInfo, SelectedMaster);
        }
        private void DoServerGetHudInfo(MyEntity blockEntity, long playerId, ulong steamId)
        {
            var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
            // get info
            var SelectedMaster = GetSelectedMaster(playerId);
            var CurrentMaster = blockLogic.GetBlockMaster();
            var NumBlocks = blockLogic.BlockArrayCount();

            // send info back to client
            SendActionToClient(blockEntity, 0, steamId, BlockAction.GetHudInfo, SelectedMaster, CurrentMaster, NumBlocks);
        }
        private void DoServerSetAppearance(MyEntity blockEntity, ulong steamId, string OffColorString, string OnColorString, string OffPatternString, string OnPatternString)
        {
            var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
            Logger.Instance.LogMessage("Patterns for block: " + blockEntity.EntityId + " COff: " + OffColorString + " COn: " + OnColorString + " POff: " + OffPatternString + " POn: " + OnPatternString);
            blockLogic.SetFieldArrayCustomData(OffColorString, OnColorString, OffPatternString, OnPatternString);
        }
        private void DoServerSetOffColor(MyEntity blockEntity, ulong steamId, Color color)
        {
            var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
            // send request to FieldArrays for processing
            blockLogic.SetAllFieldOffColor(color);
        }
        private void DoServerGetTerminalValues(MyEntity blockEntity, ulong steamId)
        {
            if (isOnline)
            {
                // get block settings
                ForceFieldSetting settings = blockEntity.GameLogic.GetAs<ForceField>().LoadTerminalValuesFromStorage();
                Logger.Instance.LogMessage("Settings For Block: " + blockEntity.EntityId + ": " + ((settings != null) ? settings.ToString() : "null"));

                // if we have settings to share (if this was not called for a block that was placed by a non server client)
                if (settings != null)
                {
                    // send enabled state as a fail safe
                    settings.Enabled = (blockEntity as IMyFunctionalBlock).Enabled;
                    // send settings to client
                    SendActionToClient(blockEntity, 0, steamId, BlockAction.GetTerminalValues, settings);
                }
                else
                {
                    // we have no settings wait until server inits block
                    // log block so settings can be sent later
                    if (PreInitBlockSettings.ContainsKey(steamId))
                    {
                        // check for this Entity
                        Dictionary<string, ForceFieldSetting> entry = PreInitBlockSettings[steamId];
                        entry.Add(blockEntity.EntityId.ToString(), null);
                    }
                    else
                    {
                        Dictionary<string, ForceFieldSetting> entry = new Dictionary<string, ForceFieldSetting>();
                        entry.Add(blockEntity.EntityId.ToString(), settings);
                        PreInitBlockSettings.Add(steamId, entry);
                    }
                }
            }
        }
        private void DoServerSelectMaster(MyEntity blockEntity, long playerId, ulong steamId)
        {
            // if this block's Master is already the Selected Master
            if (SelectedMasterID.ContainsKey(playerId))
            {
                var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
                if (SelectedMasterID[playerId].Equals(blockLogic.GetBlockMaster()))
                    return;
            }
              
              
            if (blockEntity.EntityId != 0)
            {
                var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
                SendMessageToClient(steamId, "Selected Master: " + (MyAPIGateway.Entities.GetEntityById(Convert.ToInt64(blockLogic.GetBlockMaster())) as IMyUpgradeModule).CustomName, false);

                Logger.Instance.LogMessage("New Master: " + blockLogic.GetBlockMaster() + " For Player: " + playerId);
                if (SelectedMasterID.ContainsKey(playerId))
                {
                    SelectedMasterID[playerId] = blockLogic.GetBlockMaster();
                }
                else
                {
                    // no player entry found make a new one
                    SelectedMasterID.Add(playerId, blockLogic.GetBlockMaster());
                }

                // send hud updates to client
                // if we sent the command
                if (playerId == PlayerId)
                {
                    // update hud info
                    GetPlacementHUDInfo();
                    GetHUDInfo(blockEntity as IMyTerminalBlock);
                }
                else
                {
                    // get hud info and send to client
                    DoServerGetPlacementHudInfo(playerId, steamId);
                    DoServerGetHudInfo(blockEntity, playerId, steamId);
                }
            }
        }
        private void DoServerRemoveBlock(MyEntity blockEntity, long playerId, ulong steamId)
        {
            if (blockEntity.EntityId != 0)
            {
                var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
                // check if this is the last block in its array
                if (blockLogic.BlockArrayCount() == 1)
                {
                    // do nothing

                }
                else
                {
                    SendMessageToClient(steamId, "Removing Block: " + (blockEntity as IMyUpgradeModule).CustomName, false);
                    // remove the block from its current array
                    blockLogic.RemoveBlockFromArray();

                    // remove selected master
                    SetSelctedMaster("", playerId, false);

                    // add the block to its own array
                    blockLogic.AddBlockToArray(playerId);

                    // Send to all players to remove this block
                    SendToAllSlaveOrMaster(blockEntity.EntityId, playerId, true);

                    // send hud updates to client
                    // if we sent the command
                    if (playerId == PlayerId)
                    {
                        // update hud info
                        //GetPlacementHUDInfo();
                        GetHUDInfo(blockEntity as IMyTerminalBlock);
                    }
                    else
                    {
                        // get hud info and send to client
                        //DoServerGetPlacementHudInfo(playerId, steamId);
                        DoServerGetHudInfo(blockEntity, playerId, steamId);
                    }
                }
            }
        }
        private void DoServerSetSlave(MyEntity blockEntity, long playerId, ulong steamId)
        {
            if (blockEntity.EntityId != 0)
            {
                var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
                if (GetSelectedMaster(playerId).Equals(""))
                {
                    //SendMessageToClient(steamId, "No Master Selected.", true);
                }
                else
                {
                    // check if this block does not already belongs to the selected master
                    if (!blockLogic.GetBlockMaster().Equals(GetSelectedMaster(playerId)))
                    {
                        SendMessageToClient(steamId, "Setting " + (blockEntity as IMyUpgradeModule).CustomName + " as a Slave of: " + (MyAPIGateway.Entities.GetEntityById(Convert.ToInt64(GetSelectedMaster(playerId))) as IMyUpgradeModule).CustomName, false);

                        // remove the block from its current array
                        blockLogic.RemoveBlockFromArray();

                        // add the block to the selcted master array
                        blockLogic.AddBlockToArray(playerId, GetSelectedMaster(playerId));

                        // send to all clients that this is a slave of the selected master
                        SendToAllSlaveOrMaster(blockEntity.EntityId, playerId, false, GetSelectedMaster(playerId));

                        // send hud updates to client
                        // if we sent the command
                        if (playerId == PlayerId)
                        {
                            // update hud info              
                            GetHUDInfo(blockEntity as IMyTerminalBlock);
                        }
                        else
                        {
                            // get hud info and send to client
                            DoServerGetHudInfo(blockEntity, playerId, steamId);
                        }
                    }
                }
            }
        }

        private void SendActionToAllClients(IMyEntity entityblock, byte[] MessageBytes)
        {
            var localSteamId = MyAPIGateway.Multiplayer.MyId;
            List<IMyPlayer> Players = new List<IMyPlayer>();
            MyAPIGateway.Multiplayer.Players.GetPlayers(Players, (player) => { return true; });
            var distSq = MyAPIGateway.Session.SessionSettings.SyncDistance;
            distSq += 3000; // some safety padding, avoid desync
            distSq *= distSq;
            foreach (var p in Players)
            {
                var id = p.SteamUserId;
                if (id != localSteamId && Vector3D.DistanceSquared(p.GetPosition(), entityblock.PositionComp.WorldAABB.Center) <= distSq)
                    MyAPIGateway.Multiplayer.SendMessageTo(_SND_DATATOCLIENT, MessageBytes, p.SteamUserId, true);
            }
        }
        private void SendActionToClient(IMyEntity entityblock, long playerId, ulong steamId, BlockAction action, ForceFieldSetting settings = null)
        {
            try
            {
                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = entityblock.EntityId;

                msg.PlayerId = playerId;
                msg.SteamId = steamId;
                msg.Action = (ushort)action;

                // compile block data
                if (settings != null)
                {
                    // send passed values
                    msg.OffColorR = settings.OffColor.R;
                    msg.OffColorG = settings.OffColor.G;
                    msg.OffColorB = settings.OffColor.B;
                    msg.OffColorA = settings.OffColor.A;
                    msg.OffColorString = settings.OffColorString;
                    msg.OnColorString = settings.OnColorString;
                    msg.OffPatternString = settings.OffPatternString;
                    msg.OnPatternString = settings.OnPatternString;
                    msg.Enabled = settings.Enabled;
                }

                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                MyAPIGateway.Multiplayer.SendMessageTo(_SND_DATATOCLIENT, msgBytes, msg.SteamId, true);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(ex.Message);
                Logger.Instance.LogMessage(ex.StackTrace);
            }

        }
        private void SendActionToClient(IMyEntity entityblock, long playerId, ulong steamId, BlockAction action, bool IsMaster, string MasterID = "")
        {
            NetworkMessage msg = new NetworkMessage();
            msg.BlockEntityId = entityblock.EntityId;

            msg.PlayerId = playerId;
            msg.SteamId = steamId;
            msg.Action = (ushort)action;

            // compile block data
            msg.MasterBlock = IsMaster;
            msg.MasterID = MasterID;


            Logger.Instance.LogMessage("Send message to client: " + msg.SteamId + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);


            var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

            MyAPIGateway.Multiplayer.SendMessageTo(_SND_DATATOCLIENT, msgBytes, msg.SteamId, true);
        }
        private void SendActionToClient(long playerId, ulong steamId, BlockAction action, string SelectedMaster)
        {
            NetworkMessage msg = new NetworkMessage();
            msg.BlockEntityId = 0;

            msg.PlayerId = playerId;
            msg.SteamId = steamId;
            msg.Action = (ushort)action;

            // compile block data
            msg.SelectedMaster = SelectedMaster;


            Logger.Instance.LogMessage("Send message to client: " + msg.SteamId + " Action: " + msg.Action);


            var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

            MyAPIGateway.Multiplayer.SendMessageTo(_SND_DATATOCLIENT, msgBytes, msg.SteamId, true);
        }
        private void SendActionToClient(IMyEntity entityblock, long playerId, ulong steamId, BlockAction action, string SelectedMaster, string CurrentMaster, int NumBlocks)
        {
            NetworkMessage msg = new NetworkMessage();
            msg.BlockEntityId = entityblock.EntityId;

            msg.PlayerId = playerId;
            msg.SteamId = steamId;
            msg.Action = (ushort)action;

            // compile block data
            msg.SelectedMaster = SelectedMaster;
            msg.CurrentMaster = CurrentMaster;
            msg.NumBlocks = NumBlocks;

            Logger.Instance.LogMessage("Send message to client: " + msg.SteamId + " Action: " + msg.Action);


            var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

            MyAPIGateway.Multiplayer.SendMessageTo(_SND_DATATOCLIENT, msgBytes, msg.SteamId, true);
        }
        private void SendMessageToClient(ulong playerId, string message, bool red)
        {
            if ((MyAPIGateway.Multiplayer.MyId == playerId || playerId == 0))
            {
                ShowMessage(message, red ? MyFontEnum.Red : MyFontEnum.White);
            }
            else if (isServer)
            {
                MyAPIGateway.Multiplayer.SendMessageTo(red ? _SND_REDMESSAGE : _SND_WHITEMESSAGE, Encoding.UTF8.GetBytes(message), playerId, true);
            }
        }
        private void SendWhiteMessageToClient(ulong playerId, string message) => SendMessageToClient(playerId, message, false);
        private void SendRedMessageToClient(ulong playerId, string message) => SendMessageToClient(playerId, message, true);


        // Public Handles
        // Server Only
        public void SendTerminalValues(long EntityID)
        {
            if (isServer && isOnline)
            {
                // check for any uninitialized clients for this block
                bool Send = false;
                ulong steamId = 0;
                foreach (var entry in PreInitBlockSettings)
                {
                    // if this entry has this entity ID in it
                    if (entry.Value.ContainsKey(EntityID.ToString()))
                    {
                        // this is the client we need to send to
                        steamId = entry.Key;
                        Send = true;
                        // break the loop
                        break;
                    }
                }

                // if we have a client ot send to
                if (Send)
                {
                    Logger.Instance.LogMessage("Block: " + EntityID + " Settings Need to be Sent.");
                    // collect data for sending
                    MyEntity blockEntity = MyAPIGateway.Entities.GetEntityById(EntityID) as MyEntity;
                    // get block settings
                    ForceFieldSetting settings = blockEntity.GameLogic.GetAs<ForceField>().LoadTerminalValuesFromStorage();

                    // send settings to client
                    SendActionToClient(blockEntity, 0, steamId, BlockAction.GetTerminalValues, settings);

                    // remove setting from data list
                    (PreInitBlockSettings[steamId]).Remove(EntityID.ToString());
                    if ((PreInitBlockSettings[steamId]).Count == 0)
                    {
                        PreInitBlockSettings.Remove(steamId);
                    }
                }
            }
        }
        public string GetSelectedMaster(long playerId)
        {
            if (SelectedMasterID.ContainsKey(playerId))
            {
                return SelectedMasterID[playerId];
            }
            // no player entry found make a new one
            SelectedMasterID.Add(playerId, "");
            return "";
        }
        public Dictionary<long, string> GetSelectedMasters()
        {
            return SelectedMasterID;
        }
        public void SendToAllSlaveOrMaster(long EntityID,long playerId, bool Master, string MasterID = "")
        {
            if (isOnline)
            {
                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = EntityID;

                msg.PlayerId = playerId;
                msg.SteamId = 0;
                msg.Action = (ushort)BlockAction.SetSlaveOrMaster;

                // compile block data
                msg.MasterBlock = Master;
                msg.MasterID = MasterID;


                Logger.Instance.LogMessage("Send message to all clients: " + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);


                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                SendActionToAllClients(block, msgBytes);
            }
        }
        public void SendToAllBlockOffColor(long EntityID, Color color)
        {
            if (isOnline)
            {
                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = EntityID;

                msg.PlayerId = 0;
                msg.SteamId = 0;
                msg.Action = (ushort)BlockAction.SetOffColor;

                // compile block data
                msg.OffColorR = color.R;
                msg.OffColorG = color.G;
                msg.OffColorB = color.B;
                msg.OffColorA = color.A;

                Logger.Instance.LogMessage("Send message to all clients: " + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);


                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                SendActionToAllClients(block, msgBytes);
            }
        }
        public void SendToAllBlockAppearance(long EntityID, string OffColorString, string OnColorString, string OffPatternString, string OnPatternString)
        {
            if (isOnline)
            {
                var block = MyAPIGateway.Entities.GetEntityById(EntityID);

                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = EntityID;

                msg.PlayerId = 0;
                msg.SteamId = 0;
                msg.Action = (ushort)BlockAction.SetAppearance;

                // compile block data
                msg.OffColorString = OffColorString;
                msg.OnColorString = OnColorString;
                msg.OffPatternString = OffPatternString;
                msg.OnPatternString = OnPatternString;

                Logger.Instance.LogMessage("Patterns for block: " + block.EntityId + " COff: " + OffColorString + " COn: " + OnColorString + " POff: " + OffPatternString + " POn: " + OnPatternString);
                Logger.Instance.LogMessage("Send message to all clients: " + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);


                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                
                SendActionToAllClients(block, msgBytes);
            }
        }

        // Server and Client
        public void SetBlockAppearance(long EntityID, string OffColorString, string OnColorString, string OffPatternString, string OnPatternString)
        {
            var block = MyAPIGateway.Entities.GetEntityById(EntityID);
            if (!isServer)
            {
                // send command to server
                ForceFieldSetting settings = new ForceFieldSetting();
                settings.OffColorString = OffColorString;
                settings.OnColorString = OnColorString;
                settings.OffPatternString = OffPatternString;
                settings.OnPatternString = OnPatternString;
                Logger.Instance.LogMessage("Patterns for block: " + block.EntityId + " COff: " + OffColorString + " COn: " + OnColorString + " POff: " + OffPatternString + " POn: " + OnPatternString);
                SendToServer(block, PlayerId, BlockAction.SetAppearance, settings);
            }
            else
            {
                var blockLogic = block.GameLogic.GetAs<ForceField>();
                // send request to FieldArrays for processing
                blockLogic.SetFieldArrayCustomData(OffColorString, OnColorString, OffPatternString, OnPatternString);
            }
        }
        public void SetOffColor(long EntityID, Color color)
        {
            var block = MyAPIGateway.Entities.GetEntityById(EntityID);
            if (!isServer)
            {
                // send command to server
                ForceFieldSetting settings = new ForceFieldSetting();
                settings.OffColor = color;
                SendToServer(block, PlayerId, BlockAction.SetOffColor, settings);
            }
            else
            {
                var blockLogic = block.GameLogic.GetAs<ForceField>();
                // send request to FieldArrays for processing
                blockLogic.SetAllFieldOffColor(color);
            }
        }
        public void GetTerminalValues(long EntityID)
        {
            // get terminal values
            var block = MyAPIGateway.Entities.GetEntityById(EntityID);
            if (!isServer)
            {
                SendToServer(block, PlayerId, BlockAction.GetTerminalValues);
            }
            else
            {
                // get block settings
                block.GameLogic.GetAs<ForceField>().SetTerminalValues(null);
            }
        }
        public void SetSelctedMaster(string EntityID, long playerId, bool ShowMessage)
        {
            MyEntity blockEntity = null;
            if (!EntityID.Equals(""))
                blockEntity = MyAPIGateway.Entities.GetEntityById(Convert.ToInt64(EntityID)) as MyEntity;
            if (ShowMessage)
            {
                if (!isServer)
                {
                    SendToServer(blockEntity, PlayerId, BlockAction.SelectMaster);
                }
                else
                {
                    var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
                    // if this block's Master is already the Selected Master
                    if (SelectedMasterID.ContainsKey(playerId))
                    {
                        if (SelectedMasterID[playerId].Equals(blockLogic.GetBlockMaster()))
                            return;
                    }

                    ulong steamID = 0;

                    // if passed playerID is not my PlayerID
                    if (playerId != PlayerId)
                    {
                        // get steamID of player
                        Logger.Instance.LogMessage("Player ID " + playerId + " not mine");
                        List<IMyPlayer> Players = new List<IMyPlayer>();
                        MyAPIGateway.Multiplayer.Players.GetPlayers(Players, (player) => { return (player.IdentityId == playerId); });
                        foreach(var player in Players)
                        {
                            Logger.Instance.LogMessage("Found Player: " + player.SteamUserId);
                            steamID = player.SteamUserId;
                            break;
                        }
                    }
                    TryDoServerAction(blockEntity, playerId, steamID, BlockAction.SelectMaster,null);
                }
            }
            else
            {
                if (blockEntity != null)
                {
                    var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
                    // if this block's Master is already the Selected Master
                    if (SelectedMasterID.ContainsKey(playerId))
                    {
                        if (SelectedMasterID[playerId].Equals(blockLogic.GetBlockMaster()))
                            return;
                    }
                }
                

                Logger.Instance.LogMessage("New Master: " + EntityID + " For Player: " + playerId);
                // just set the variable
                if (SelectedMasterID.ContainsKey(playerId))
                {
                    SelectedMasterID[playerId] = EntityID;
                }
                else
                {
                    // no player entry found make a new one
                    SelectedMasterID.Add(playerId, EntityID);
                }

                // send hud updates to client
                // if we sent the command
                if (playerId == PlayerId)
                {
                    // update hud info
                    GetPlacementHUDInfo();
                    //GetHUDInfo(blockEntity as IMyTerminalBlock);
                }
                else
                {
                    // get player steam info
                    ulong steamID = 0;
                    List<IMyPlayer> Players = new List<IMyPlayer>();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(Players, (player) => { return (player.IdentityId == playerId); });
                    foreach (var player in Players)
                    {
                        //Logger.Instance.LogMessage("Found Player: " + player.SteamUserId);
                        steamID = player.SteamUserId;
                        break;
                    }

                    // get hud info and send to client
                    DoServerGetPlacementHudInfo(playerId, steamID);
                    //DoServerGetHudInfo(blockEntity, playerId, steamID);
                }

            }
        }
        public void GetPlacementHUDInfo()
        {
            if (!isServer)
            {
                SendToServer(null, PlayerId, BlockAction.GetPlacementHudInfo);
            }
            else
            {
                // get hud info
                var SelectedMaster = GetSelectedMaster(PlayerId);
                UpdateHudPlacementMessage(SelectedMaster);
            }
            
        }
        public void GetHUDInfo(IMyTerminalBlock blockEntity)
        {
            if (!isServer)
            {
                SendToServer(blockEntity, PlayerId, BlockAction.GetHudInfo);
            }
            else
            {
                var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
                // get hud info
                var SelectedMaster = GetSelectedMaster(PlayerId);
                var CurrentMaster = blockLogic.GetBlockMaster();
                var NumBlocks = blockLogic.BlockArrayCount();
                UpdateHudMessage(blockEntity, SelectedMaster, CurrentMaster, NumBlocks);
            }
        }

        // Client Callbacks
        private void RecieveActionFromServer(byte[] msgBytes)
        {
            try
            {
                NetworkMessage msg = MyAPIGateway.Utilities.SerializeFromBinary<NetworkMessage>(msgBytes);

                Logger.Instance.LogMessage("Message Recieved From Server: for block: " + msg.BlockEntityId + " Action: " + msg.Action);

                if (!MyAPIGateway.Entities.EntityExists(msg.BlockEntityId) && (BlockAction)msg.Action != BlockAction.GetPlacementHudInfo)
                    return;

                var blockEntity = MyAPIGateway.Entities.GetEntityById(msg.BlockEntityId) as MyEntity;

                if (blockEntity == null && (BlockAction)msg.Action != BlockAction.GetPlacementHudInfo)
                    return;

                if (blockEntity is IMyCubeBlock || (BlockAction)msg.Action == BlockAction.GetPlacementHudInfo)
                    TryDoClientAction(blockEntity, msg.PlayerId, msg.SteamId, (BlockAction)msg.Action, msg);
            }
            catch(Exception ex)
            {
                Logger.Instance.LogMessage(ex.Message);
                Logger.Instance.LogMessage(ex.StackTrace);
            }
           
        }

        private void RecieveWhiteMessage(byte[] obj)
        {
            string str = Encoding.UTF8.GetString(obj);
            ShowMessage(str, MyFontEnum.White);
        }

        private void RecieveRedMessage(byte[] obj)
        {
            string str = Encoding.UTF8.GetString(obj);
            ShowMessage(str, MyFontEnum.Red);
        }

        private void ShowMessage(string message, string color)
        {
            MyAPIGateway.Utilities.ShowNotification(message, 2000, color);    
        }

        private void TryDoClientAction(MyEntity blockEntity, long playerId, ulong steamId, BlockAction action, NetworkMessage data)
        {
            PerformClientAction(blockEntity, playerId, steamId, action, data);
        }

        private void PerformClientAction(MyEntity blockEntity, long playerId, ulong steamId, BlockAction action, NetworkMessage data)
        {
            if (action == BlockAction.GetTerminalValues)
            {
                // decompile settings data
                ForceFieldSetting settings = new ForceFieldSetting();
                settings.OffColor = new Color(data.OffColorR, data.OffColorG, data.OffColorB, data.OffColorA);
                settings.OffColorString = data.OffColorString;
                settings.OnColorString = data.OnColorString;
                settings.OffPatternString = data.OffPatternString;
                settings.OnPatternString = data.OnPatternString;
                settings.Enabled = data.Enabled;
                DoClientGetTerminalValues(blockEntity, settings);
            }
            else if (action == BlockAction.SetSlaveOrMaster)
            {
                // decompile data
                DoClientSetSlaveOrMaster(blockEntity, playerId, data.MasterBlock, data.MasterID);
            }   
            else if (action == BlockAction.SetOffColor)
            {
                // decompile data
                Color color = Color.FromNonPremultiplied(data.OffColorR, data.OffColorG, data.OffColorB, data.OffColorA);
                DoClientSetOffColor(blockEntity, color);
            }
            else if (action == BlockAction.SetAppearance)
            {
                DoClientSetAppearance(blockEntity, steamId, data.OffColorString, data.OnColorString, data.OffPatternString, data.OnPatternString);
            }
            else if (action == BlockAction.GetPlacementHudInfo)
            {
                DoClientGetPlacementHudInfo(data.SelectedMaster);
            }
            else if (action == BlockAction.GetHudInfo)
            {
                DoClientGetHudInfo(blockEntity as IMyTerminalBlock, data.SelectedMaster, data.CurrentMaster, data.NumBlocks);
            }
        }

        private void DoClientGetHudInfo(IMyTerminalBlock blockEntity, string SelectedMaster, string CurrentMaster, int NumBlocks)
        {
            // update hud info
            UpdateHudMessage(blockEntity, SelectedMaster, CurrentMaster, NumBlocks);
        }
        private void DoClientGetPlacementHudInfo(string SelectedMaster)
        {
            // update placement hud info
            UpdateHudPlacementMessage(SelectedMaster);
        }
        private void DoClientSetAppearance(MyEntity blockEntity, ulong steamId, string OffColorString, string OnColorString, string OffPatternString, string OnPatternString)
        {
            Logger.Instance.LogMessage("Patterns for block: " + blockEntity.EntityId + " COff: " + OffColorString + " COn: " + OnColorString + " POff: " + OffPatternString + " POn: " + OnPatternString);
            // send request to FieldArrays for processing
            blockEntity.GameLogic.GetAs<ForceField>().SetFieldSlaveCustomData(OffColorString, OnColorString, OffPatternString, OnPatternString);
        }
        private void DoClientSetOffColor(MyEntity blockEntity, Color color)
        {
            // send data to block
            blockEntity.GameLogic.GetAs<ForceField>().SetForceFieldOffColor(color);
        }
        private void DoClientGetTerminalValues(MyEntity blockEntity, ForceFieldSetting settings)
        {
            // send settings to block
            blockEntity.GameLogic.GetAs<ForceField>().SetTerminalValues(settings);
        }
        private void DoClientSetSlaveOrMaster(MyEntity blockEntity, long playerId, bool Master, string MasterID)
        {
            var blockLogic = blockEntity.GameLogic.GetAs<ForceField>();
            // if this block is a master
            if (Master)
            {
                // remove the block from its current array
                blockLogic.RemoveBlockFromArray();

                // add the block to its own array
                blockLogic.AddBlockToArray(playerId);
            }
            else
            {
                // remove the block from its current array
                blockLogic.RemoveBlockFromArray();

                // add the block to the selcted master array
                blockLogic.AddBlockToArray(PlayerId, MasterID);
            }
        }
    }

    [ProtoContract]
    public class NetworkMessage
    {
        [ProtoMember(1,IsRequired = true,Name = "BlockID")]
        public long BlockEntityId;
        [ProtoMember(2, IsRequired = true, Name = "PlayerID")]
        public long PlayerId;
        [ProtoMember(3, IsRequired = true, Name = "steamID")]
        public ulong SteamId;
        [ProtoMember(4, IsRequired = true, Name = "Action")]
        public ushort Action;

        [ProtoMember(5, IsRequired = false, Name = "OffColorR")]
        public byte OffColorR;
        [ProtoMember(6, IsRequired = false, Name = "OffColorG")]
        public byte OffColorG;
        [ProtoMember(7, IsRequired = false, Name = "OffColorB")]
        public byte OffColorB;
        [ProtoMember(8, IsRequired = false, Name = "OffColorA")]
        public byte OffColorA;

        [ProtoMember(9, IsRequired = false, Name = "OffColorString")]
        public string OffColorString;
        [ProtoMember(10, IsRequired = false, Name = "OnColorString")]
        public string OnColorString;
        [ProtoMember(11, IsRequired = false, Name = "OffPatternString")]
        public string OffPatternString;
        [ProtoMember(12, IsRequired = false, Name = "OnPatternString")]
        public string OnPatternString;

        [ProtoMember(13, IsRequired = false, Name = "MasterID")]
        public string MasterID;
        [ProtoMember(14, IsRequired = false, Name = "MasterBlock")]
        public bool MasterBlock;
        [ProtoMember(15, IsRequired = false, Name = "Enabled")]
        public bool Enabled;
        [ProtoMember(16, IsRequired = false, Name = "BrokenNames")]
        public List<string> BrokenNames;

        [ProtoMember(17, IsRequired = false, Name = "SelectedMaster")]
        public string SelectedMaster;
        [ProtoMember(18, IsRequired = false, Name = "CurrentMaster")]
        public string CurrentMaster;
        [ProtoMember(19, IsRequired = false, Name = "NumBlocks")]
        public int NumBlocks;
    }
}
