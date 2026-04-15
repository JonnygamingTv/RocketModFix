using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Collections.Generic;

namespace Rocket.Core.Logging
{
    public partial class Logger
    {
        // ── Assembly-name cache ───────────────────────────────────────────────
        // Assembly.GetName() allocates a new AssemblyName object every call.
        // Assembly references are stable for the lifetime of the process, so we
        // cache the name string the first time we see each assembly.
        private static readonly Dictionary<Assembly, string> _assemblyNameCache
            = new Dictionary<Assembly, string>(32);

        // Pre-interned constant strings used in prefix building.
        private static readonly string _suffix = " >> ";
        private static readonly string _rocketPrefix = "Rocket.";

        // These assembly names are treated as "transparent" – we skip past them
        // when walking the stack to find the real caller.
        private static readonly string _asmCSharp = "Assembly-CSharp";
        private static readonly string _asmUnity = "UnityEngine";
        private static readonly string _asmSelf = typeof(Logger).Assembly.GetName().Name;

        // Last prefix shown – stored as the full "Name >> " string (or "") so we
        // can reuse it without rebuilding it. Volatile so off-thread writers see
        // the latest value without a lock (last-write-wins is fine for a cosmetic hint).
        private volatile static string _lastPrefix = "";

        // ── Obsolete stub ────────────────────────────────────────────────────
        [Obsolete("Log(string message, bool sendToConsole) is obsolete, use Log(string message, ConsoleColor color) instead", true)]
        public static void Log(string message, bool sendToConsole)
            => Log(message, ConsoleColor.White);

        // ── Main log entry point ─────────────────────────────────────────────
        public static void Log(string message, ConsoleColor color = ConsoleColor.White)
        {
            if (message == null) return;

            // Build the "AssemblyName >> " prefix by walking at most 3 stack frames.
            // We capture the StackTrace once and reuse it – cheaper than multiple
            // GetFrame() calls on a freshly created trace each time.
            string prefix = "";
            try
            {
                StackTrace st = new StackTrace();
                int frameCount = st.FrameCount;

                if (frameCount > 1)
                {
                    string name = GetAssemblyName(st.GetFrame(1));

                    // If frame 1 is Rocket/Unity internals, try frame 2.
                    if (frameCount > 2 && IsTransparentAssembly(name))
                        name = GetAssemblyName(st.GetFrame(2));

                    // Suppress the prefix entirely for internal/repeated assemblies.
                    if (!string.IsNullOrEmpty(name)
                        && !IsTransparentAssembly(name)
                        && name != _asmSelf)
                    {
                        prefix = name + _suffix;     // "PluginName >> "
                    }
                }

                _lastPrefix = prefix;
            }
            catch
            {
                // Stack-trace capture can fail in some AOT/stripped environments.
                // Fall back to no prefix rather than crashing the log call.
                prefix = _lastPrefix;
            }

            // Avoid allocating a new string when there is no prefix.
            string final = prefix.Length == 0 ? message : prefix + message;
            ProcessInternalLog(ELogType.Info, final, color);
        }

        // ── Exception logging ─────────────────────────────────────────────────
        public static void LogException(Exception ex, string message = null)
        {
            if (ex == null) return;

            string source = "";
            string prefix = "";
            try
            {
                StackTrace st = new StackTrace();
                int frameCount = st.FrameCount;

                if (frameCount > 1)
                {
                    StackFrame f1 = st.GetFrame(1);
                    string name = GetAssemblyName(f1);

                    if (name.StartsWith(_rocketPrefix) && frameCount > 2)
                    {
                        StackFrame f2 = st.GetFrame(2);
                        source = f2.GetMethod()?.Name ?? "";
                        name = GetAssemblyName(f2);
                    }
                    else
                    {
                        source = f1.GetMethod()?.Name ?? "";
                    }

                    _lastPrefix = name;
                    if (!string.IsNullOrEmpty(name)) prefix = name + _suffix;
                }
            }
            catch
            {
                LogError("Caught exception while logging an exception! Ouch...");
            }

            // Build the final message with minimal allocations.
            // Using string.Concat avoids intermediate StringBuilder for this fixed shape.
            string body = message != null
                ? string.Concat(prefix, message, " -> Exception in ", source, ": ", ex.ToString())
                : string.Concat(prefix, "Exception in ", source, ": ", ex.ToString());

            ProcessInternalLog(ELogType.Exception, body);
        }

        // ── Convenience wrappers ─────────────────────────────────────────────
        public static void Log(Exception ex) => LogException(ex);

        internal static void LogError(Exception ex, string v) => LogException(ex, v);

        public static void LogWarning(string message)
        {
            if (message == null) return;
            ProcessInternalLog(ELogType.Warning, message);
        }

        public static void LogError(string message)
        {
            if (message == null) return;
            ProcessInternalLog(ELogType.Error, message);
        }

        // ── Diagnostic helper ─────────────────────────────────────────────────
        // var_dump is a debug/diagnostic tool, not a hotpath.
        // Main improvement: avoid allocating a new StringBuilder per property just
        // to build the indent string; use a small char-repeat helper instead.
        internal static string var_dump(object obj, int recursion)
        {
            StringBuilder result = new StringBuilder();
            if (recursion >= 5) return "";

            Type t = obj.GetType();
            PropertyInfo[] properties = t.GetProperties();
            const string spaces = "|   ";
            const string trail = "|...";

            foreach (PropertyInfo property in properties)
            {
                try
                {
                    object value = property.GetValue(obj, null);
                    string indent = recursion > 0
                        ? RepeatString(spaces, recursion - 1) + trail
                        : "";

                    if (value != null)
                    {
                        string displayValue = value is string
                            ? string.Concat("\"", value.ToString(), "\"")
                            : value.ToString();

                        result.AppendFormat("{0}{1} = {2}\n", indent, property.Name, displayValue);

                        try
                        {
                            if (value is ICollection col)
                            {
                                string innerIndent = RepeatString(spaces, recursion) + trail;
                                int elementCount = 0;
                                foreach (object element in col)
                                {
                                    string elementName = string.Format("{0}[{1}]", property.Name, elementCount++);
                                    result.AppendFormat("{0}{1} = {2}\n", innerIndent, elementName, element.ToString());
                                    result.Append(var_dump(element, recursion + 2));
                                }
                                result.Append(var_dump(value, recursion + 1));
                            }
                            else
                            {
                                result.Append(var_dump(value, recursion + 1));
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        result.AppendFormat("{0}{1} = null\n", indent, property.Name);
                    }
                }
                catch
                {
                    // Some properties throw on GetValue – intentionally swallowed.
                }
            }

            return result.ToString();
        }

        // ── Internal pipeline ─────────────────────────────────────────────────
        private static void ProcessInternalLog(ELogType type, string message,
                                               ConsoleColor color = ConsoleColor.White)
        {
            if (type == ELogType.Error || type == ELogType.Exception)
                SDG.Unturned.CommandWindow.LogError(message);
            else if (type == ELogType.Warning)
                SDG.Unturned.CommandWindow.LogWarning(message);
            else
                SDG.Unturned.CommandWindow.Log(message);
        }

        private static void ProcessLog(ELogType type, string message, bool rcon = true)
        {
            AsyncLoggerQueue.Current.Enqueue(new LogEntry() { Severity = type, Message = message, RCON = rcon });
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void ExternalLog(object message, ConsoleColor color)
        {
            ELogType severity;
            switch (color)
            {
                case ConsoleColor.Red: severity = ELogType.Error; break;
                case ConsoleColor.Yellow: severity = ELogType.Warning; break;
                default: severity = ELogType.Info; break;
            }
            ProcessLog(severity, message.ToString());
        }

        // ── Private helpers ───────────────────────────────────────────────────

        // Returns the cached assembly name for a given stack frame.
        // Returns "" on any failure so callers never have to null-check.
        private static string GetAssemblyName(StackFrame frame)
        {
            try
            {
                Assembly asm = frame?.GetMethod()?.DeclaringType?.Assembly;
                if (asm == null) return "";

                if (_assemblyNameCache.TryGetValue(asm, out string cached))
                    return cached;

                string name = asm.GetName().Name ?? "";
                _assemblyNameCache[asm] = name;
                return name;
            }
            catch
            {
                return "";
            }
        }

        // Returns true for assemblies we skip past when hunting for the real caller.
        private static bool IsTransparentAssembly(string name)
            => string.IsNullOrEmpty(name)
            || name == _asmSelf
            || name == _asmCSharp
            || name == _asmUnity
            || name.StartsWith(_rocketPrefix);

        // Cheap fixed-count string repeat without LINQ or StringBuilder overhead.
        // Only used in var_dump (diagnostic path), so a small loop is fine.
        private static string RepeatString(string s, int count)
        {
            if (count <= 0) return "";
            if (count == 1) return s;
            StringBuilder sb = new StringBuilder(s.Length * count);
            for (int i = 0; i < count; i++) sb.Append(s);
            return sb.ToString();
        }
    }
}