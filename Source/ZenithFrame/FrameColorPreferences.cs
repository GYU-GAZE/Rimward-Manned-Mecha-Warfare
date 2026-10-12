using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public sealed class FrameColorPreferences : ModSettings
    {
        public List<string> favorites = new List<string>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref favorites, "favoriteFrameColors", LookMode.Value);
            if (favorites == null) favorites = new List<string>();
        }
    }

    public sealed class ZenithFrameMod : Mod
    {
        public static ZenithFrameMod Instance { get; private set; }
        public FrameColorPreferences Colors { get; }

        public ZenithFrameMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Colors = GetSettings<FrameColorPreferences>();
        }
    }

    public static class FrameHexColor
    {
        private const string Digits = "0123456789ABCDEF";

        public static string Format(Color color)
        {
            Color32 bytes = color;
            return "#" + bytes.r.ToString("X2") + bytes.g.ToString("X2") + bytes.b.ToString("X2");
        }

        public static bool TryParse(string input, out Color color)
        {
            color = Color.white;
            if (input == null) return false;
            string hex = input.Trim().ToUpperInvariant();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return false;
            foreach (char digit in hex)
                if (Digits.IndexOf(digit) < 0) return false;
            color = new Color32(
                (byte)(Digits.IndexOf(hex[0]) * 16 + Digits.IndexOf(hex[1])),
                (byte)(Digits.IndexOf(hex[2]) * 16 + Digits.IndexOf(hex[3])),
                (byte)(Digits.IndexOf(hex[4]) * 16 + Digits.IndexOf(hex[5])), 255);
            return true;
        }
    }
}
