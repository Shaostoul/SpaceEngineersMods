#if DEBUG
#define PROFILE
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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
using SpaceEngineers.Game.ModAPI;

namespace BlueKnight.TurretSpotlight
{
    [ProtoContract]
    public class TurretSetting
    {
        [ProtoMember]
        public long EntityId = 0;

        [ProtoMember]
        public Color LightColor = new Color(0, 150, 255, 255);

        [ProtoMember]
        public float LightRadius = 120;

        [ProtoMember]
        public float LightIntensity = 4;

        [ProtoMember]
        public float LightOffset = 0.5f;

        [ProtoMember]
        public float LightBlinkInterval = 0;

        [ProtoMember]
        public float LightBlinkLength = 10;

        [ProtoMember]
        public float LightBlinkOffset = 0;

        [ProtoMember]
        public float TurretAzimuth = 0;

        [ProtoMember]
        public float TurretElevation = 0;

        [ProtoMember]
        public bool Enabled = false;
    }
    public static class TurretSettingExtensions
    {
        public static TurretSetting RetrieveTerminalValues(this IMyLargeMissileTurret turret)
        {
            return TurretSpotlightSettings.Instance.RetrieveTerminalValues(turret);
        }

        public static void StoreTerminalValues(this IMyLargeMissileTurret turret)
        {
            TurretSpotlightSettings.Instance.StoreTerminalValues(turret);
        }

        public static void DeleteTerminalValues(this IMyLargeMissileTurret turret)
        {
            TurretSpotlightSettings.Instance.DeleteTerminalValues(turret);
        }
    }
    public class TurretSpotlightSettings
    {
        static bool _initialized = false;
        static TurretSpotlightSettings _instance = new TurretSpotlightSettings();
        public static TurretSpotlightSettings Instance
        {
            get
            {
                if (!_initialized)
                    Init();
                return _instance;
            }
        }

        private TurretSpotlightSettings()
        {
        }

        private static void Init()
        {
            try
            {
                _instance.LoadAllTerminalValues();
                _initialized = true;
            }
            catch
            {
                // ignore

            }
        }
        List<TurretSetting> m_TurretSettings = new List<TurretSetting>();

        // This is to save data to world file
        public void SaveAllTerminalValues()
        {
            Logger.Instance.LogDebug("SaveAllTerminalValues");
            try
            {
                var strdata = MyAPIGateway.Utilities.SerializeToXML<List<TurretSetting>>(m_TurretSettings);
                MyAPIGateway.Utilities.SetVariable<string>("BlueKnight.TurretSpotlight.turret", strdata);
            }
            catch (Exception ex)
            {
                // If an old save game is loaded, it seems it might try to resave to upgrade.
                // If this happens, the ModAPI may not be initialized
                // NEVER prevent someone from saving their game.
                // It's better to lose terminal information than a player to lose hours of work.
                Logger.Instance.LogMessage("WARNING: There was an error saving terminal settings. Values may be lost.");
                Logger.Instance.LogMessage(ex.Message);
                Logger.Instance.LogMessage(ex.StackTrace);
            }
        }
        public void LoadAllTerminalValues()
        {
            Logger.Instance.LogDebug("LoadAllTerminalValues");

            string strdata;
            MyAPIGateway.Utilities.GetVariable<string>("BlueKnight.TurretSpotlight.turret", out strdata);
            if (!string.IsNullOrEmpty(strdata))
            {
                Logger.Instance.LogDebug("Success!");
                m_TurretSettings = MyAPIGateway.Utilities.SerializeFromXML<List<TurretSetting>>(strdata);
            }
        }

        public TurretSetting RetrieveTerminalValues(IMyLargeMissileTurret turret)
        {
            Logger.Instance.LogDebug("RetrieveTerminalValues");
            var settings = m_TurretSettings.FirstOrDefault((x) => x.EntityId == turret.EntityId);

            return settings;
        }
        public void StoreTerminalValues(IMyLargeMissileTurret turret)
        {
            var settings = m_TurretSettings.FirstOrDefault((x) => x.EntityId == turret.EntityId);

            if (settings == null)
            {
                settings = new TurretSetting();
                settings.EntityId = turret.EntityId;
                m_TurretSettings.Add(settings);
            }
            settings.LightColor = turret.GameLogic.GetAs<TurretSpotlight>().Light_Color;
            settings.LightRadius = turret.GameLogic.GetAs<TurretSpotlight>().Light_Radius;
            settings.LightIntensity = turret.GameLogic.GetAs<TurretSpotlight>().Light_Intensity;
            settings.LightOffset = turret.GameLogic.GetAs<TurretSpotlight>().Light_Offset;
            settings.LightBlinkInterval = turret.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Interval;
            settings.LightBlinkLength = turret.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Length;
            settings.LightBlinkOffset = turret.GameLogic.GetAs<TurretSpotlight>().Light_Blink_Offset;
            settings.TurretAzimuth = turret.GameLogic.GetAs<TurretSpotlight>().Turret_Azimuth;
            settings.TurretElevation = turret.GameLogic.GetAs<TurretSpotlight>().Turret_Elevation;
        }
        public void DeleteTerminalValues(IMyLargeMissileTurret turret)
        {
            Logger.Instance.LogDebug("DeleteTerminalValues");
            var settings = m_TurretSettings.FirstOrDefault((x) => x.EntityId == turret.EntityId);

            if (settings != null)
            {
                TurretSpotlightSettings.Instance.m_TurretSettings.Remove(settings);
            }
        }
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    class MissionComponent : MySessionComponentBase
    {
        public override void LoadData()
        {
            Logger.Instance.Init("TurretSpotlight");
            Logger.Instance.Debug = true;
        }
        protected override void UnloadData()
        {
            Logger.Instance.Close();
        }

        public override void SaveData()
        {
            TurretSpotlightSettings.Instance.SaveAllTerminalValues();
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
        private const ushort _SND_DATATOCLIENT = 10804;

        private bool init = false;
        public bool isDedicated = false;
        public bool isServer = false;
        public bool isOnline = false;

        public static PlayerInput Instance;
       
        private Dictionary<ulong, Dictionary<string, TurretSetting>> PreInitBlockSettings = new Dictionary<ulong, Dictionary<string, TurretSetting>>();

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
            GetTerminalValues,
            SetColor,
            SetIntensity,
            SetOffset,
            SetRadius
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

            if (isServer && MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE)
                MyAPIGateway.Multiplayer.RegisterMessageHandler(_RCV_FROMCLIENT, RecieveFromClient);

            //if (isDedicated)
            {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(_SND_DATATOCLIENT, RecieveActionFromServer);
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
            }
        }

        private void SendToServer(IMyEntity entityblock, long playerID, BlockAction action, TurretSetting settings = null)
        {
            try
            {
                if (playerID == 0)
                    return;

                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = entityblock.EntityId;

                msg.PlayerId = playerID;
                msg.SteamId = MyAPIGateway.Multiplayer.MyId;
                msg.Action = (ushort)action;
                // compile block data
                if (settings != null)
                {
                    // send passed values
                    msg.ColorR = settings.LightColor.R;
                    msg.ColorG = settings.LightColor.G;
                    msg.ColorB = settings.LightColor.B;
                    msg.ColorA = settings.LightColor.A;
                    msg.LightIntensity = settings.LightIntensity;
                    msg.LightOffset = settings.LightOffset;
                    msg.LightRadius = settings.LightRadius;
                    msg.LightBlinkInterval = settings.LightBlinkInterval;
                    msg.LightBlinkLength = settings.LightBlinkLength;
                    msg.LightBlinkOffset = settings.LightBlinkOffset;
                    msg.TurretAzimuth = settings.TurretAzimuth;
                    msg.TurretElevation = settings.TurretElevation;
                    msg.Enabled = settings.Enabled;
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

                if (!MyAPIGateway.Entities.EntityExists(msg.BlockEntityId))
                    return;

                var blockEntity = MyAPIGateway.Entities.GetEntityById(msg.BlockEntityId) as MyEntity;

                if (blockEntity == null)
                    return;

                if (blockEntity is IMyCubeBlock)
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
                default:
                    Logger.Instance.LogMessage("Action Taken. PlayerID: " + playerId + " SteamID: " + steamId + " Action: " + action + " BlockID: " + blockEntity.EntityId);
                    // any other action require no permissions
                    PerformServerAction(blockEntity, playerId, steamId, action, data);
                    break;
            }


        }

        private void PerformServerAction(MyEntity blockEntity, long playerId, ulong steamId, BlockAction action, NetworkMessage data)
        {
            if (action == BlockAction.GetTerminalValues)
            {
                DoServerGetTerminalValues(blockEntity, steamId);
            }
            else if (action == BlockAction.SetColor)
            {
                Color color = Color.FromNonPremultiplied(data.ColorR, data.ColorG, data.ColorB, data.ColorA);
                DoServerSetColor(blockEntity, steamId, color);
            }
            else if (action == BlockAction.SetIntensity)
            {
                DoServerSetIntensity(blockEntity, steamId, data.LightIntensity);
            }
            else if (action == BlockAction.SetOffset)
            {
                DoServerSetOffset(blockEntity, steamId, data.LightOffset);
            }
            else if (action == BlockAction.SetRadius)
            {
                DoServerSetRadius(blockEntity, steamId, data.LightRadius);
            }
        }

        private void DoServerSetColor(MyEntity blockEntity, ulong steamId, Color color)
        {
            // send color change to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().SetTurretLightColor(color);

            // send color change to other clients
            SendToAllBlockColor(blockEntity.EntityId, color);
        }
        private void DoServerSetIntensity(MyEntity blockEntity, ulong steamId, float intensity)
        {
            // send intensity change to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().SetTurretLightIntensity(intensity);

            // send intensity change to other clients
            SendToAllBlockIntensity(blockEntity.EntityId, intensity);
        }
        private void DoServerSetRadius(MyEntity blockEntity, ulong steamId, float radius)
        {
            // send radius change to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().SetTurretLightRadius(radius);

            // send radius change to other clients
            SendToAllBlockRadius(blockEntity.EntityId, radius);
        }
        private void DoServerSetOffset(MyEntity blockEntity, ulong steamId, float offset)
        {
            // send offset change to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().SetTurretLightOffset(offset);

            // send offset change to other clients
            SendToAllBlockOffset(blockEntity.EntityId, offset);
        }
        private void DoServerGetTerminalValues(MyEntity blockEntity, ulong steamId)
        {
            if (isOnline)
            {
                // get block settings
                TurretSetting settings = (blockEntity as IMyLargeMissileTurret).RetrieveTerminalValues();
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
                        Dictionary<string, TurretSetting> entry = PreInitBlockSettings[steamId];
                        entry.Add(blockEntity.EntityId.ToString(), null);
                    }
                    else
                    {
                        Dictionary<string, TurretSetting> entry = new Dictionary<string, TurretSetting>();
                        entry.Add(blockEntity.EntityId.ToString(), settings);
                        PreInitBlockSettings.Add(steamId, entry);
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
        private void SendActionToClient(IMyEntity entityblock, long playerId, ulong steamId, BlockAction action, TurretSetting settings = null)
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
                    msg.ColorR = settings.LightColor.R;
                    msg.ColorG = settings.LightColor.G;
                    msg.ColorB = settings.LightColor.B;
                    msg.ColorA = settings.LightColor.A;
                    msg.LightIntensity = settings.LightIntensity;
                    msg.LightOffset = settings.LightOffset;
                    msg.LightRadius = settings.LightRadius;
                    msg.LightBlinkInterval = settings.LightBlinkInterval;
                    msg.LightBlinkLength = settings.LightBlinkLength;
                    msg.LightBlinkOffset = settings.LightBlinkOffset;
                    msg.TurretAzimuth = settings.TurretAzimuth;
                    msg.TurretElevation = settings.TurretElevation;
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
                    TurretSetting settings = (blockEntity as IMyLargeMissileTurret).RetrieveTerminalValues();

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
        public void SaveTerminalValues(long EntityID)
        {
            // save terminal values      
            if (isServer)
            {
                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                // store block settings
                (block as IMyLargeMissileTurret).StoreTerminalValues();
            }
        }
        public void DeleteTerminalValues(long EntityID)
        {
            // delete terminal values  
            if (isServer)
            {
                // delete block settings
                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                (block as IMyLargeMissileTurret).DeleteTerminalValues();
            }
        }      
        public void SendToAllBlockColor(long EntityID, Color color)
        {
            if (isOnline)
            {
                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = EntityID;

                msg.PlayerId = 0;
                msg.SteamId = 0;
                msg.Action = (ushort)BlockAction.SetColor;

                // compile block data
                msg.ColorR = color.R;
                msg.ColorG = color.G;
                msg.ColorB = color.B;
                msg.ColorA = color.A;

                Logger.Instance.LogMessage("Send message to all clients: " + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);


                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                SendActionToAllClients(block, msgBytes);
            }
        }
        public void SendToAllBlockIntensity(long EntityID, float intensity)
        {
            if (isOnline)
            {
                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = EntityID;

                msg.PlayerId = 0;
                msg.SteamId = 0;
                msg.Action = (ushort)BlockAction.SetIntensity;

                // compile block data
                msg.LightIntensity = intensity;

                Logger.Instance.LogMessage("Send message to all clients: " + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);


                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                SendActionToAllClients(block, msgBytes);
            }
        }
        public void SendToAllBlockOffset(long EntityID, float offset)
        {
            if (isOnline)
            {
                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = EntityID;

                msg.PlayerId = 0;
                msg.SteamId = 0;
                msg.Action = (ushort)BlockAction.SetOffset;

                // compile block data
                msg.LightOffset = offset;

                Logger.Instance.LogMessage("Send message to all clients: " + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);


                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                SendActionToAllClients(block, msgBytes);
            }
        }
        public void SendToAllBlockRadius(long EntityID, float radius)
        {
            if (isOnline)
            {
                NetworkMessage msg = new NetworkMessage();
                msg.BlockEntityId = EntityID;

                msg.PlayerId = 0;
                msg.SteamId = 0;
                msg.Action = (ushort)BlockAction.SetRadius;

                // compile block data
                msg.LightRadius = radius;

                Logger.Instance.LogMessage("Send message to all clients: " + " for block: " + msg.BlockEntityId + " Action: " + msg.Action);


                var msgBytes = MyAPIGateway.Utilities.SerializeToBinary(msg);

                var block = MyAPIGateway.Entities.GetEntityById(EntityID);
                SendActionToAllClients(block, msgBytes);
            }
        }

        // Server and Client
        public void SetColor(long EntityID, Color color)
        {
            var block = MyAPIGateway.Entities.GetEntityById(EntityID);
            if (!isServer)
            {
                // send command to server
                TurretSetting settings = new TurretSetting();
                settings.LightColor = color;
                SendToServer(block, PlayerId, BlockAction.SetColor, settings);
            }
            else
            {
                // send color change to block
                block.GameLogic.GetAs<TurretSpotlight>().SetTurretLightColor(color);
            }
        }
        public void SetIntensity(long EntityID, float intensity)
        {
            var block = MyAPIGateway.Entities.GetEntityById(EntityID);
            if (!isServer)
            {
                // send command to server
                TurretSetting settings = new TurretSetting();
                settings.LightIntensity = intensity;
                SendToServer(block, PlayerId, BlockAction.SetIntensity, settings);
            }
            else
            {
                // send intensity change to block
                block.GameLogic.GetAs<TurretSpotlight>().SetTurretLightIntensity(intensity);
            }
        }
        public void SetRadius(long EntityID, float radius)
        {
            var block = MyAPIGateway.Entities.GetEntityById(EntityID);
            if (!isServer)
            {
                // send command to server
                TurretSetting settings = new TurretSetting();
                settings.LightRadius = radius;
                SendToServer(block, PlayerId, BlockAction.SetRadius, settings);
            }
            else
            {
                // send radius change to block
                block.GameLogic.GetAs<TurretSpotlight>().SetTurretLightRadius(radius);
            }
        }
        public void SetOffset(long EntityID, float offset)
        {
            var block = MyAPIGateway.Entities.GetEntityById(EntityID);
            if (!isServer)
            {
                // send command to server
                TurretSetting settings = new TurretSetting();
                settings.LightOffset = offset;
                SendToServer(block, PlayerId, BlockAction.SetOffset, settings);
            }
            else
            {
                // send offset change to block
                block.GameLogic.GetAs<TurretSpotlight>().SetTurretLightOffset(offset);
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
                TurretSetting settings = (block as IMyLargeMissileTurret).RetrieveTerminalValues();
                Logger.Instance.LogMessage("Settings For Block: " + block.EntityId + ": " + ((settings != null) ? settings.ToString() : "null"));
                // send settings to block
                block.GameLogic.GetAs<TurretSpotlight>().InitTerminalValues(settings);
            }
        }
       

        // Client Callbacks
        private void RecieveActionFromServer(byte[] msgBytes)
        {
            try
            {
                NetworkMessage msg = MyAPIGateway.Utilities.SerializeFromBinary<NetworkMessage>(msgBytes);

                Logger.Instance.LogMessage("Message Recieved From Server: for block: " + msg.BlockEntityId + " Action: " + msg.Action);

                if (!MyAPIGateway.Entities.EntityExists(msg.BlockEntityId))
                    return;

                var blockEntity = MyAPIGateway.Entities.GetEntityById(msg.BlockEntityId) as MyEntity;

                if (blockEntity == null)
                    return;

                if (blockEntity is IMyCubeBlock)
                    TryDoClientAction(blockEntity, msg.PlayerId, msg.SteamId, (BlockAction)msg.Action, msg);
            }
            catch(Exception ex)
            {
                Logger.Instance.LogMessage(ex.Message);
                Logger.Instance.LogMessage(ex.StackTrace);
            }
           
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
                TurretSetting settings = new TurretSetting();
                settings.LightColor = new Color(data.ColorR, data.ColorG, data.ColorB, data.ColorA);
                settings.LightIntensity = data.LightIntensity;
                settings.LightOffset = data.LightOffset;
                settings.LightRadius = data.LightRadius;
                settings.LightBlinkInterval = data.LightBlinkInterval;
                settings.LightBlinkLength = data.LightBlinkLength;
                settings.LightBlinkOffset = data.LightBlinkOffset;
                settings.TurretAzimuth = data.TurretAzimuth;
                settings.TurretElevation = data.TurretElevation;
                settings.Enabled = data.Enabled;
                DoClientGetTerminalValues(blockEntity, settings);
            }        
            else if (action == BlockAction.SetColor)
            {
                // decompile data
                Color color = Color.FromNonPremultiplied(data.ColorR, data.ColorG, data.ColorB, data.ColorA);
                DoClientSetColor(blockEntity, color);
            }
            else if (action == BlockAction.SetIntensity)
            {
                DoClientSetIntensity(blockEntity, data.LightIntensity);
            }
            else if (action == BlockAction.SetOffset)
            {
                DoClientSetOffset(blockEntity, data.LightOffset);
            }
            else if (action == BlockAction.SetRadius)
            {
                DoClientSetRadius(blockEntity, data.LightRadius);
            }
        }

        private void DoClientSetColor(MyEntity blockEntity, Color color)
        {
            // send data to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().SetTurretLightColor(color);
        }
        private void DoClientSetIntensity(MyEntity blockEntity, float intensity)
        {
            // send data to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().SetTurretLightIntensity(intensity);
        }
        private void DoClientSetRadius(MyEntity blockEntity, float radius)
        {
            // send data to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().SetTurretLightRadius(radius);
        }
        private void DoClientSetOffset(MyEntity blockEntity, float offset)
        {
            // send data to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().SetTurretLightOffset(offset);
        }
        private void DoClientGetTerminalValues(MyEntity blockEntity, TurretSetting settings)
        {
            // send settings to block
            blockEntity.GameLogic.GetAs<TurretSpotlight>().InitTerminalValues(settings);
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

        [ProtoMember(5, IsRequired = false, Name = "ColorR")]
        public byte ColorR;
        [ProtoMember(6, IsRequired = false, Name = "ColorG")]
        public byte ColorG;
        [ProtoMember(7, IsRequired = false, Name = "ColorB")]
        public byte ColorB;
        [ProtoMember(8, IsRequired = false, Name = "ColorA")]
        public byte ColorA;

        [ProtoMember(9, IsRequired = false, Name = "LightRadius")]
        public float LightRadius = 120;
        [ProtoMember(10, IsRequired = false, Name = "LightIntensity")]
        public float LightIntensity = 4;
        [ProtoMember(11, IsRequired = false, Name = "LightOffset")]
        public float LightOffset = 0.5f;
        [ProtoMember(12, IsRequired = false, Name = "LightBlinkInterval")]
        public float LightBlinkInterval = 0;

        [ProtoMember(13, IsRequired = false, Name = "LightBlinkLength")]
        public float LightBlinkLength = 10;
        [ProtoMember(14, IsRequired = false, Name = "LightBlinkOffset")]
        public float LightBlinkOffset = 0;
        [ProtoMember(15, IsRequired = false, Name = "Enabled")]
        public bool Enabled;
        [ProtoMember(16, IsRequired = false, Name = "TurretAzimuth")]
        public float TurretAzimuth = 0;
        [ProtoMember(17, IsRequired = false, Name = "TurretElevation")]
        public float TurretElevation = 0;
    }
}
