using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ItemStorageComponent
{
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    public static class RegisterAndCheckVersion
    {
        private static void Prefix(ZNetPeer peer, ref ZNet __instance)
        {
            // Register version check call
            ItemStorageComponentPlugin.ItemStorageComponentLogger.LogDebug("Registering version RPC handler");
            peer.m_rpc.Register($"{ItemStorageComponentPlugin.ModName}_VersionCheck",
                new Action<ZRpc, ZPackage>(RpcHandlers.RPC_ItemStorageComponent_Version));

            // Make calls to check versions
            ItemStorageComponentPlugin.ItemStorageComponentLogger.LogInfo("Invoking version check");
            ZPackage zpackage = new();
            zpackage.Write(ItemStorageComponentPlugin.ModVersion);
            peer.m_rpc.Invoke($"{ItemStorageComponentPlugin.ModName}_VersionCheck", zpackage);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
    public static class VerifyClient
    {
        private static bool Prefix(ZRpc rpc, ZPackage pkg, ref ZNet __instance)
        {
            if (!__instance.IsServer() || RpcHandlers.ValidatedPeers.Contains(rpc)) return true;
            // Disconnect peer if they didn't send mod version at all
            ItemStorageComponentPlugin.ItemStorageComponentLogger.LogWarning(
                $"Peer ({rpc.m_socket.GetHostName()}) never sent version or couldn't due to previous disconnect, disconnecting");
            rpc.Invoke("Error", 3);
            return false; // Prevent calling underlying method
        }

        private static void Postfix(ZNet __instance)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), "RequestAdminSync",
                new ZPackage());
        }
    }

    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.ShowConnectError))]
    public class ShowConnectionError
    {
        private static void Postfix(FejdStartup __instance)
        {
            if (__instance.m_connectionFailedPanel.activeSelf)
                __instance.m_connectionFailedError.text += "\n" + ItemStorageComponentPlugin.ConnectionError;
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
    public static class RemoveDisconnectedPeerFromVerified
    {
        private static void Prefix(ZNetPeer peer, ref ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            // Remove peer from validated list
            ItemStorageComponentPlugin.ItemStorageComponentLogger.LogInfo(
                $"Peer ({peer.m_rpc.m_socket.GetHostName()}) disconnected, removing from validated list");
            _ = RpcHandlers.ValidatedPeers.Remove(peer.m_rpc);
        }
    }

    public static class RpcHandlers
    {
        public static readonly List<ZRpc> ValidatedPeers = new();

        public static void RPC_ItemStorageComponent_Version(ZRpc rpc, ZPackage pkg)
        {
            string? version = pkg.ReadString();
            ItemStorageComponentPlugin.ItemStorageComponentLogger.LogInfo("Version check, local: " +
                                                                          ItemStorageComponentPlugin.ModVersion +
                                                                          ",  remote: " + version);
            if (version != ItemStorageComponentPlugin.ModVersion)
            {
                ItemStorageComponentPlugin.ConnectionError =
                    $"{ItemStorageComponentPlugin.ModName} Installed: {ItemStorageComponentPlugin.ModVersion}\n Needed: {version}";
                if (!ZNet.instance.IsServer()) return;
                // Different versions - force disconnect client from server
                ItemStorageComponentPlugin.ItemStorageComponentLogger.LogWarning(
                    $"Peer ({rpc.m_socket.GetHostName()}) has incompatible version, disconnecting");
                rpc.Invoke("Error", 3);
            }
            else
            {
                if (!ZNet.instance.IsServer())
                {
                    // Enable mod on client if versions match
                    ItemStorageComponentPlugin.ItemStorageComponentLogger.LogInfo(
                        "Received same version from server!");
                }
                else
                {
                    // Add client to validated list
                    ItemStorageComponentPlugin.ItemStorageComponentLogger.LogInfo(
                        $"Adding peer ({rpc.m_socket.GetHostName()}) to validated list");
                    ValidatedPeers.Add(rpc);
                }
            }
        }
    }
}