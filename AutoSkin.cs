using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Ext.AutoKit;
using Oxide.Ext.AutoKit.Messages;
using Oxide.Ext.AutoKit.Models;
using Oxide.Ext.AutoKit.Settings;

namespace Oxide.Plugins
{
    [Info( "AutoSkin", "kwamaking", "1.0.0" )]
    [Description( "Automatically apply a saved skin set to your inventory." )]
    class AutoSkin : RustPlugin
    {
        private const string UsePermission = "autoskin.use";
        private const int NoSkin = 0;
        private const ulong JetpackSkin = 2632956407;
        private AutoKit<ItemSkin> autoKit { get; set; }
        private AutoSkinMessages messages { get; set; }

        #region Oxide Hooks

        void OnServerInitialized()
        {
            messages = new AutoSkinMessages();
            autoKit = new AutoKit<ItemSkin>( messages, new AutoKitSettings( pluginName: this.Name, iconId: 76561198955675901) );
            permission.RegisterPermission( UsePermission, this );
            cmd.AddChatCommand( "as", this, "AutoSkinCommand" );
        }

        void OnServerSave()
        {
            autoKit.Save();
        }

        void Unload()
        {
            autoKit.Save();
        }

        private void OnKitRedeemed( BasePlayer player, string kitName )
        {
            autoKit.With( player, ( action ) => action.WithKit( kitName ).MaybeApply( Apply ) );
        }

        private void OnKitSaved( BasePlayer player, string kitName )
        {
            autoKit.With( player, ( action ) => action.WithNewKit( kitName ).MaybeSave( Save ).Notify() );
            autoKit.Save();
        }

        private void OnKitRemoved( BasePlayer player, string kitName )
        {
            autoKit.With( player, ( action ) => action.WithKit( kitName ).MaybeRemove() );
        }

        #endregion

        #region Commands

        [ChatCommand( "autoskin" )]
        void AutoSkinCommand( BasePlayer player, string command, string[] args )
        {
            try
            {
                if ( !permission.UserHasPermission( player.UserIDString, UsePermission ) )
                {
                    autoKit.With( player, ( action ) => action.ToNonDestructive().WithNotification( messages.noPermission ).ToNotify().Notify() );
                    return;
                }
                var run = args.ElementAtOrDefault( 0 ) ?? "help";
                var kitName = args.ElementAtOrDefault( 1 ) ?? run;
                switch ( run )
                {
                    case "save":
                        autoKit.With( player, ( action ) => action.WithNewKit( kitName ).Save( Save ).Notify() );
                        autoKit.Save();
                        break;
                    case "help":
                        autoKit.With( player, ( action ) => action.ToNonDestructive().WithNotification( messages.help ).ToNotify().Notify() );
                        break;
                    case "list":
                        autoKit.With( player, ( action ) => action.ToNonDestructive().ListToNotify().Notify() );
                        break;
                    case "remove":
                        autoKit.With( player, ( action ) => action.WithKit( kitName ).Remove().Notify() );
                        break;
                    case "random":
                        ApplyRandomKit( player );
                        break;
                    default:
                        autoKit.With( player, ( action ) => action.WithKit( kitName ).Apply( Apply ).Notify() );
                        break;
                }
            }
            catch ( Exception e )
            {
                Puts( $"Failed to run AutoSkin: {e.Message}" );
            }
        }

        #endregion

        #region AutoSkin

        private void Apply( BasePlayer player, Kit<ItemSkin> kit )
        {
            if ( null == kit ) return;
            var playerInventory = player.inventory.AllItems().ToList();
            kit.items.ForEach( kitItem =>
            {
                playerInventory.FindAll( item => item.info.itemid == kitItem.id )?.ForEach( item =>
                {
                    UpdateItemSkin( player, item, kitItem.skinId );
                } );
            } );
        }

        private Kit<ItemSkin> Save( BasePlayer player, Kit<ItemSkin> kit )
        {
            var playerInventory = player.inventory.AllItems().ToList();

            kit.items = playerInventory
            .FindAll( item => item.skin != NoSkin && item.skin != JetpackSkin )
            .ConvertAll( item => new ItemSkin { id = item.info.itemid, skinId = item.skin } );

            return kit;
        }

        private void ApplyRandomKit( BasePlayer player )
        {
            autoKit.With( player, ( action ) =>
            {
                action.WithNewKit( $"random: {player.UserIDString}" ).Apply( ( _, kit ) =>
                   {
                       player.inventory.AllItems().ToList().ForEach( item =>
                       {
                           var randomSkin = FindRandomSkin( player, ItemManager.FindItemDefinition( item.info.shortname ) );
                           UpdateItemSkin( player, item, randomSkin );
                       } );
                   } ).Notify();
            } );
        }

        private void UpdateItemSkin( BasePlayer player, Item item, ulong skinId )
        {
            item.skin = skinId;
            var held = item.GetHeldEntity();
            item.MarkDirty();

            if ( held != null )
            {
                held.skinID = item.skin;
                held.SendNetworkUpdateImmediate();
            }

            if ( player.GetActiveItem()?.info?.itemid == item.info.itemid )
            {
                player.UpdateActiveItem( item.uid );
                player.EnsureUpdated();
            }

        }

        private ulong FindRandomSkin( BasePlayer player, ItemDefinition itemDefinition )
        {
            var skins = new List<ulong>();
            Interface.CallHook( "OnFetchSkins", player, itemDefinition, skins );

            return skins.GetRandom();
        }

        #endregion

        #region Configuration Classes

        public class ItemSkin
        {
            [JsonProperty( "id" )]
            public int id { get; set; }
            [JsonProperty( "skinId" )]
            public ulong skinId { get; set; }
        }

        public class AutoSkinMessages : AutoKitMessages
        {
            public string noPermission { get; set; } = "You do not have permission to use AutoSkin.";
            public string random { get; set; } = "Random skins have been applied to your inventory.";
            public string help { get; set; } =
                "\n<color=green>/autoskin <kit></color>. - apply a saved skin kit to your inventory, \n" +
                "<color=green>/autoskin random <kit></color> - apply random skins to your inventory, \n" +
                "<color=green>/autoskin save <kit></color> - Save the skins applied to your inventory, \n" +
                "<color=green>/autoskin list</color> - List your saved skin kits, \n" +
                "<color=green>/autoskin remove <kit></color> - Remove a saved skin kit, \n" +
                "<color=green>/autoskin help</color> - To see this message again.\n" +
                "<color=green>/as</color> Command shortcut.\n ";
        }

        #endregion
    }
}
