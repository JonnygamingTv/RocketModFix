using Rocket.API;
using Rocket.Core;
using Rocket.Core.Logging;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace Rocket.Unturned.Chat
{
    public sealed class UnturnedChat : MonoBehaviour
    {
        // ── Colour conversion constants ──────────────────────────────────────
        // Precomputed so GetColorFromRGB never does a division at call-site.
        private const float INV_255 = 1f / 255f;
        private const float INV_100 = 1f / 100f;

        // ── Named colour lookup ──────────────────────────────────────────────
        // Static dictionary built once; OrdinalIgnoreCase so no ToLower() allocation
        // per call. "rocket" is the only non-Unity named colour.
        private static readonly Dictionary<string, Color> _namedColors =
            new Dictionary<string, Color>(13, StringComparer.OrdinalIgnoreCase)
            {
                { "black",   Color.black   },
                { "blue",    Color.blue    },
                { "clear",   Color.clear   },
                { "cyan",    Color.cyan    },
                { "gray",    Color.gray    },
                { "green",   Color.green   },
                { "grey",    Color.grey    },
                { "magenta", Color.magenta },
                { "red",     Color.red     },
                { "white",   Color.white   },
                { "yellow",  Color.yellow  },
                { "rocket",  new Color(90 * INV_255, 206 * INV_255, 205 * INV_255, 1f) },
            };

        // ── wrapMessage reusable builder ─────────────────────────────────────
        // [ThreadStatic] means each thread gets its own instance (no lock needed).
        // FixedUpdate/Say are main-thread-only in practice, but this is defensive.
        [ThreadStatic]
        private static StringBuilder _wrapBuilder;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            ChatManager.onChatted += handleChat;
        }

        // ── Chat handler ─────────────────────────────────────────────────────
        private void handleChat(SteamPlayer steamPlayer, EChatMode chatMode,
                                ref Color incomingColor, ref bool rich,
                                string message, ref bool isVisible)
        {
            bool cancel = !isVisible;
            Color color = incomingColor;
            try
            {
                UnturnedPlayer player = UnturnedPlayer.FromSteamPlayer(steamPlayer);
                color = UnturnedPlayerEvents.firePlayerChatted(
                    player, chatMode, player.Color, message, ref cancel);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
            isVisible = !cancel;
            incomingColor = color;
        }

        // ── Colour helpers ───────────────────────────────────────────────────
        public static Color GetColorFromName(string colorName, Color fallback)
        {
            // Trim once (user input may have whitespace); no ToLower() allocation –
            // the dictionary uses OrdinalIgnoreCase.
            string trimmed = colorName.Trim();
            return _namedColors.TryGetValue(trimmed, out Color c) ? c :
                   GetColorFromHex(trimmed) ?? fallback;
        }

        public static Color? GetColorFromHex(string hexString)
        {
            // Strip leading '#' without allocating via Replace.
            int start = hexString.Length > 0 && hexString[0] == '#' ? 1 : 0;
            int len = hexString.Length - start;

            // We need exactly 6 nibbles to parse. Expand 3-char shorthand in a
            // fixed char[6] — one small heap alloc, still far cheaper than the
            // three string.Insert calls in the original.
            // (Span<char>/stackalloc not available in netstandard2.1 without unsafe.)
            char[] buf;
            if (len == 3)
            {
                buf = new char[6]
                {
                    hexString[start],     hexString[start],
                    hexString[start + 1], hexString[start + 1],
                    hexString[start + 2], hexString[start + 2],
                };
            }
            else if (len == 6)
            {
                buf = new char[6];
                for (int i = 0; i < 6; i++) buf[i] = hexString[start + i];
            }
            else
            {
                return null;
            }

            // Parse hex manually – avoids Int32.TryParse overhead and NumberStyles.
            int argb = 0;
            for (int i = 0; i < 6; i++)
            {
                char ch = buf[i];
                int nibble;
                if (ch >= '0' && ch <= '9') nibble = ch - '0';
                else if (ch >= 'a' && ch <= 'f') nibble = ch - 'a' + 10;
                else if (ch >= 'A' && ch <= 'F') nibble = ch - 'A' + 10;
                else return null;
                argb = (argb << 4) | nibble;
            }

            return GetColorFromRGB(
                (byte)((argb >> 16) & 0xff),
                (byte)((argb >> 8) & 0xff),
                (byte)(argb & 0xff));
        }

        public static Color GetColorFromRGB(byte R, byte G, byte B)
            => new Color(R * INV_255, G * INV_255, B * INV_255, 1f);

        public static Color GetColorFromRGB(byte R, byte G, byte B, short A)
            => new Color(R * INV_255, G * INV_255, B * INV_255, A * INV_100);

        // ── Say overloads ────────────────────────────────────────────────────
        public static void Say(string message)
            => Say(message, Palette.SERVER, false);

        public static void Say(string message, bool rich)
            => Say(message, Palette.SERVER, rich);

        public static void Say(string message, Color color)
            => Say(message, color, false);

        public static void Say(string message, Color color, bool rich)
        {
            Logger.Log("Broadcast: " + message, ConsoleColor.Gray);
            List<string> lines = wrapMessage(message);
            for (int i = 0; i < lines.Count; i++)
                ChatManager.serverSendMessage(lines[i], color,
                    fromPlayer: null, toPlayer: null,
                    mode: EChatMode.GLOBAL, iconURL: null,
                    useRichTextFormatting: rich);
        }

        public static void Say(IRocketPlayer player, string message)
            => Say(player, message, Palette.SERVER, false);

        public static void Say(IRocketPlayer player, string message, bool rich)
            => Say(player, message, Palette.SERVER, rich);

        public static void Say(IRocketPlayer player, string message, Color color)
            => Say(player, message, color, false);

        public static void Say(IRocketPlayer player, string message, Color color, bool rich)
        {
            if (player is ConsolePlayer)
                Logger.Log(message, ConsoleColor.Gray);
            else
                Say(new CSteamID(ulong.Parse(player.Id)), message, color, rich);
        }

        public static void Say(CSteamID id, string message)
            => Say(id, message, Palette.SERVER, false);

        public static void Say(CSteamID id, string message, bool rich)
            => Say(id, message, Palette.SERVER, rich);

        public static void Say(CSteamID id, string message, Color color)
            => Say(id, message, color, false);

        public static void Say(CSteamID id, string message, Color color, bool rich)
        {
            // CSteamID(0) is the invalid/nil ID – compare the ulong directly,
            // no ToString() allocation.
            if (id == CSteamID.Nil)
            {
                Logger.Log(message, ConsoleColor.Gray);
                return;
            }

            SteamPlayer toPlayer = PlayerTool.getSteamPlayer(id);
            List<string> lines = wrapMessage(message);
            for (int i = 0; i < lines.Count; i++)
                ChatManager.serverSendMessage(lines[i], color,
                    fromPlayer: null, toPlayer: toPlayer,
                    mode: EChatMode.SAY, iconURL: null,
                    useRichTextFormatting: rich);
        }

        // ── Message wrapper ──────────────────────────────────────────────────
        // Splits a message into lines no longer than ChatManager.MAX_MESSAGE_LENGTH.
        // Uses a [ThreadStatic] StringBuilder to avoid per-call heap allocation for
        // the accumulating line buffer. Word splitting uses index arithmetic on the
        // original string rather than string.Split (no array, no per-word allocation).
        public static List<string> wrapMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>(0);

            int maxLen = ChatManager.MAX_MESSAGE_LENGTH;
            List<string> lines = new List<string>();

            // Reuse or create the builder for this thread.
            StringBuilder sb = _wrapBuilder ?? (_wrapBuilder = new StringBuilder(maxLen + 16));
            sb.Clear();

            int i = 0;
            while (i < text.Length)
            {
                // Find the end of the next word.
                int wordStart = i;
                while (i < text.Length && text[i] != ' ') i++;
                int wordLen = i - wordStart;

                // Skip the space separator.
                if (i < text.Length) i++;

                if (sb.Length > 0)
                {
                    // Would adding " word" overflow the line?
                    if (sb.Length + 1 + wordLen > maxLen)
                    {
                        lines.Add(sb.ToString());
                        sb.Clear();
                        sb.Append(text, wordStart, wordLen);
                    }
                    else
                    {
                        sb.Append(' ');
                        sb.Append(text, wordStart, wordLen);
                    }
                }
                else
                {
                    // First word on a fresh line – append even if it alone exceeds
                    // maxLen (matches original behaviour; we don't split mid-word).
                    sb.Append(text, wordStart, wordLen);
                }
            }

            if (sb.Length > 0)
                lines.Add(sb.ToString());

            return lines;
        }
    }
}