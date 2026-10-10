using System.Collections.Generic;
using Candide.CandideUI.ChatWindow;
using Candide.GameModels.Managers;
using Candide.Multiplayer.Chat;
using CandideServer.MessageModels;
using CandideServer.Models;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace BetterCarts.Patches;

// applied only when a feature failed to load
internal static class FailureNoticePatch {
    private static bool _pending;

    // runs on every world load and join
    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.ReceiveFullGameState))]
    private static class WorldLoad {
        private static void Postfix() {
            _pending = true;
        }
    }

    [HarmonyPatch(typeof(CandideChatWindow), nameof(CandideChatWindow.Update), typeof(GameTime))]
    private static class Send {
        private static void Postfix() {
            if (!_pending) {
                return;
            }
            _pending = false;
            ModLog.Watch("FailureNoticePatch.Send.Postfix", SendLine);
        }
    }

    private static void SendLine() {
        ChatLogManager.SendLocalChatMessage(ChatLogMessage.DirectText(Text(), ChatMessageType.DeveloperError,
            BetterCartsPlugin.PluginName));
    }

    private static string Text() {
        IReadOnlyList<string> names = FeatureLoader.Failed;
        string list = names[0];
        for (int i = 1; i < names.Count; i++) {
            list += (i == names.Count - 1 ? " and " : ", ") + names[i];
        }
        return list + " failed to load. Details in BepInEx/LogOutput.log";
    }
}
