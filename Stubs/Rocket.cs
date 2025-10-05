using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Rocket.API
{
    public interface IRocketPluginConfiguration
    {
        void LoadDefaults();
    }

    public interface IRocketPlayer
    {
        string Id { get; }
        string DisplayName { get; }
    }

    public enum AllowedCaller
    {
        Player,
        Console
    }

    public interface IRocketCommand
    {
        AllowedCaller AllowedCaller { get; }
        string Name { get; }
        string Help { get; }
        string Syntax { get; }
        List<string> Aliases { get; }
        List<string> Permissions { get; }
        void Execute(IRocketPlayer caller, string[] command);
    }
}

namespace Rocket.Core.Logging
{
    public static class Logger
    {
        public static void Log(string message) => Console.WriteLine(message);
        public static void LogWarning(string message) => Console.WriteLine(message);
        public static void LogError(string message) => Console.Error.WriteLine(message);
        public static void LogException(Exception exception) => Console.Error.WriteLine(exception);
    }
}

namespace Rocket.Core.Utils
{
    public static class TaskDispatcher
    {
        public static void QueueOnMainThread(Func<Task> task)
        {
            if (task == null)
            {
                return;
            }

            Task.Run(task);
        }
    }
}

namespace Rocket.Core.Plugins
{
    public abstract class RocketPlugin<TConfiguration> : MonoBehaviour where TConfiguration : Rocket.API.IRocketPluginConfiguration, new()
    {
        protected RocketPlugin()
        {
            Configuration = new ConfigurationContainer<TConfiguration>(new TConfiguration());
        }

        public ConfigurationContainer<TConfiguration> Configuration { get; }

        protected virtual void Load()
        {
        }

        protected virtual void Unload()
        {
        }
    }

    public sealed class ConfigurationContainer<TConfiguration>
        where TConfiguration : Rocket.API.IRocketPluginConfiguration
    {
        public ConfigurationContainer(TConfiguration instance)
        {
            Instance = instance;
        }

        public TConfiguration Instance { get; }
    }
}

namespace Rocket.Unturned.Chat
{
    using Rocket.Unturned.Player;

    public static class UnturnedChat
    {
        public static void Say(UnturnedPlayer player, string message, Color color)
        {
            Console.WriteLine($"[CHAT:{color.r},{color.g},{color.b}] {player?.DisplayName}: {message}");
        }
    }
}

namespace Rocket.Unturned
{
    using Rocket.Unturned.Player;

    public static class U
    {
        public static class Events
        {
            public static event Action<UnturnedPlayer> OnPlayerConnected;
            public static event Action<UnturnedPlayer> OnPlayerDisconnected;

            public static void RaisePlayerConnected(UnturnedPlayer player) => OnPlayerConnected?.Invoke(player);
            public static void RaisePlayerDisconnected(UnturnedPlayer player) => OnPlayerDisconnected?.Invoke(player);
        }
    }
}

namespace Rocket.Unturned.Player
{
    using Rocket.API;
    using Steamworks;

    public class UnturnedPlayer : IRocketPlayer
    {
        public UnturnedPlayer()
        {
        }

        public CSteamID CSteamID { get; set; }
        public string DisplayName { get; set; }
        public string CharacterName { get; set; }
        public Vector3 Position { get; set; }
        public float Rotation { get; set; }

        public string Id => CSteamID.m_SteamID.ToString();

        public static UnturnedPlayer FromCSteamID(CSteamID id) => null;

        public void Teleport(Vector3 position, float rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }
}
