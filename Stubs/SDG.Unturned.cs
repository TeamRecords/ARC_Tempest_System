using System.Collections.Generic;
using UnityEngine;
using Steamworks;

namespace SDG.Unturned
{
    public static class Provider
    {
        public static List<SteamPlayer> clients { get; } = new List<SteamPlayer>();
    }

    public class SteamPlayer
    {
        public SteamPlayer()
        {
            player = new Player();
            playerID = new PlayerID();
        }

        public Player player { get; set; }
        public PlayerID playerID { get; set; }
    }

    public class Player
    {
        public Player()
        {
            transform = new Transform();
            life = new PlayerLife();
        }

        public Transform transform { get; set; }
        public PlayerLife life { get; set; }
    }

    public class PlayerLife
    {
        public byte health { get; set; }
    }

    public class PlayerID
    {
        public CSteamID steamID;
        public string characterName;
        public string nickName;
        public string groupName;
    }

    public static class Level
    {
        public static LevelInfo info;
        public static string levelName;
        public static int size;
    }

    public class LevelInfo
    {
        public string name { get; set; }
    }
}
