using Rocket.API;
using Rocket.API.Serialisation;
using Rocket.Core.Assets;
using Rocket.Core.Logging;
using Rocket.Core.Permissions;
using Rocket.Core.Serialization;
using Rocket.Core.Utils;
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Rocket.Core.Commands
{
    public class RocketCommandManager : MonoBehaviour
    {
        private readonly HashSet<Assembly> assemblies = new HashSet<Assembly>();
        [Obsolete("commandsDict is more performant")]
        private readonly List<RegisteredRocketCommand> commands = new List<RegisteredRocketCommand>();
        private readonly Dictionary<string, RegisteredRocketCommand> commandsDict = new Dictionary<string, RegisteredRocketCommand>(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, RocketCommandCooldown> cooldown = new Dictionary<string, RocketCommandCooldown>(StringComparer.OrdinalIgnoreCase);
        [Obsolete("commandsDict is more performant")]
        public ReadOnlyCollection<RegisteredRocketCommand> Commands { get; internal set; }
        private XMLFileAsset<RocketCommands> commandMappings;

        public delegate void ExecuteCommand(IRocketPlayer player, IRocketCommand command, ref bool cancel);
        public event ExecuteCommand OnExecuteCommand;

        internal void Reload()
        {
            commandMappings.Load();
            checkCommandMappings();
            ReadOnlyCollection<RegisteredRocketCommand> tmp = commands.ToList().AsReadOnly();
            foreach (RegisteredRocketCommand ReregCmd in tmp) DeRegisterCommand(ReregCmd.Command);
            foreach (RegisteredRocketCommand ReregCmd in tmp) Register(ReregCmd.Command);
            // loop through assemblies(?) instead of re-registering commands based of previously registered.
            foreach (Assembly asm in assemblies) RegisterFromAssembly(asm);
        }
        public RocketCommandManager() { }

        private void Awake()
        {
            Commands = commands.AsReadOnly();
            commandMappings = new XMLFileAsset<RocketCommands>(Environment.CommandsFile);
            checkCommandMappings();
            R.Plugins.OnPluginsLoaded += Plugins_OnPluginsLoaded;
        }

        private void checkCommandMappings()
        {
            commandMappings.Instance.CommandMappings = commandMappings.Instance.CommandMappings.Distinct(new CommandMappingComparer()).ToList();
            checkDuplicateCommandMappings();
        }

        private void checkDuplicateCommandMappings(string classname = null) {
            foreach (CommandMapping mapping in (classname == null) ? commandMappings.Instance.CommandMappings : commandMappings.Instance.CommandMappings.Where(cm => cm.Class == classname))
            {
                string n = mapping.Name.ToLower();
                string c = mapping.Class.ToLower();

                if (mapping.Enabled)
                    foreach (CommandMapping otherMappings in commandMappings.Instance.CommandMappings.Where(m => m.Name.ToLower() == n && m.Enabled && m.Class.ToLower() != c))
                    {
                        Logging.Logger.Log("Other mapping to: "+otherMappings.Class+" / "+mapping.Class);
                        if (otherMappings.Priority > mapping.Priority)
                        {
                            mapping.Enabled = false;
                        }
                        else
                        {
                            otherMappings.Enabled = false;
                        }
                    }
            }
            commandMappings.Save();
        }

        private void Plugins_OnPluginsLoaded()
        {
            commandMappings.Save();
        }

        private IRocketCommand GetCommand(IRocketCommand command)
        {
           return GetCommand(command.Name);
        }

        public IRocketCommand GetCommand(string command)
        {
            commandsDict.TryGetValue(command, out RegisteredRocketCommand foundCommand);
// MOVED into dictionary:                foundCommand = commands.Where(c => c.Aliases.Select(a => a.ToLower()).Contains(command.ToLower())).FirstOrDefault();
            return foundCommand;
        }

        private static string getCommandIdentity(IRocketCommand command,string name)
        {
            if (command is RocketAttributeCommand)
            {
                return ((RocketAttributeCommand)command).Method.ReflectedType.FullName+"/"+ name;
            }
            else if(command.GetType().ReflectedType != null)
            {
                return command.GetType().ReflectedType.FullName + "/" + name;
            }
            else
            {
                return command.GetType().FullName+"/"+ name;
            }
        }

        private static Type getCommandType(IRocketCommand command)
        {
            if (command is RocketAttributeCommand)
            {
                return ((RocketAttributeCommand)command).Method.ReflectedType;
            }
            else if (command.GetType().ReflectedType != null)
            {
                return command.GetType().ReflectedType;
            }
            else
            {
                return command.GetType();
            }
        }



        public class CommandMappingComparer : IEqualityComparer<CommandMapping>
        {
            public bool Equals(CommandMapping x, CommandMapping y)
            {
                return (x.Name.ToLower() == y.Name.ToLower() && x.Class.ToLower() == y.Class.ToLower());
            }

            public int GetHashCode(CommandMapping obj)
            {
                return (obj.Name.ToLower()+obj.Class.ToLower()).GetHashCode();
            }
        }
        public bool Register(IRocketCommand command)
        {
            Register(command, null);
            return true;
        }

        public void Register(IRocketCommand command, string alias)
        {
            Register(command, alias, CommandPriority.Normal);
        }

        public void Register(IRocketCommand command, string alias, CommandPriority priority)
        {
            string name = command.Name;
            if (alias != null) name = alias;
            string className = getCommandIdentity(command,name);

            //Add CommandMapping if not already existing
            if(commandMappings.Instance.CommandMappings.Where(m => m.Class == className && m.Name == name).FirstOrDefault() == null){
                commandMappings.Instance.CommandMappings.Add(new CommandMapping(name,className,true,priority));
            }
            checkDuplicateCommandMappings(className);

            foreach(CommandMapping mapping in commandMappings.Instance.CommandMappings.Where(m => m.Class == className && m.Enabled))
            {
                commands.Add(new RegisteredRocketCommand(mapping.Name.ToLower(), command));
                commandsDict[mapping.Name] = new RegisteredRocketCommand(mapping.Name.ToLower(), command);
                if (command.Aliases != null)
                {
                    foreach (string Alias in command.Aliases)
                    {
                        if (string.IsNullOrEmpty(Alias)) continue;
                        if(!commandsDict.ContainsKey(Alias))
                            commandsDict[Alias] = commandsDict[mapping.Name];
                    }
                }
                Logging.Logger.Log("[registered] /" + mapping.Name.ToLower() + " (" + mapping.Class + ")", ConsoleColor.Green);
            }
        }

        public void DeregisterFromAssembly(Assembly assembly)
        {
            commands.RemoveAll(rc => getCommandType(rc.Command).Assembly == assembly);

            List<string> cmdsToRemove = new List<string>();
            foreach (string CMD in commandsDict.Keys)
            {
                if (getCommandType(commandsDict[CMD].Command).Assembly == assembly)
                    cmdsToRemove.Add(CMD);
            }
            foreach (string CMD in cmdsToRemove)
            {
                commandsDict.Remove(CMD);
            }
        }
        public void DeRegisterCommand(IRocketCommand Command)
        {
            commands.RemoveAll(rc => rc.Command == Command);

            List<string> cmdsToRemove = new List<string>();
            foreach (string CMD in commandsDict.Keys)
            {
                if (commandsDict[CMD].Command == Command)
                    cmdsToRemove.Add(CMD);
            }
            foreach (string CMD in cmdsToRemove)
            {
                commandsDict.Remove(CMD);
            }
        }

        public double GetCooldown(IRocketPlayer player, IRocketCommand command)
        {
            string key;
            if (command == null || !cooldown.TryGetValue(key = player.Id + '.' + command.Name, out RocketCommandCooldown c) || c == null) return -1;
            double timeSinceExecution = (DateTime.Now - c.CommandRequested).TotalSeconds;
            if (c.ApplyingPermission.Cooldown <= timeSinceExecution)
            {
                //Cooldown has expired
                cooldown.Remove(key);
                return -1;
            }
            else
            {
                return c.ApplyingPermission.Cooldown - (uint)timeSinceExecution;
            }
        }

        public void SetCooldown(IRocketPlayer player, IRocketCommand command)
        {
            List<Permission> applyingPermissions = R.Permissions.GetPermissions(player, command);
            Permission cooldownPermission = applyingPermissions.Where(p => p.Cooldown != 0).OrderByDescending(p => p.Cooldown).FirstOrDefault();
            if (cooldownPermission != null)
            {
                cooldown[player.Id + '.' + command.Name] = new RocketCommandCooldown(player, command, cooldownPermission);
            }
        }

        public void ClearInactiveCooldowns()
        {
            HashSet<string> IdsOnline = new HashSet<string>(SDG.Unturned.Provider.clients.Select(a => a.playerID.steamID.ToString()));
            foreach(string Id in cooldown.Keys.ToArray())
            {
                string[] split = Id.Split('.');
                if (IdsOnline.Contains(split[0])) continue; // Player is online, do not touch. To prevent race conditions if running this function async
                RocketPlayer player = new RocketPlayer(split[0]);
                IRocketCommand Cmd = GetCommand(split[1]);
                GetCooldown(player, Cmd);
            }
        }

        private static readonly char[] _spaceSeparator = { ' ' };

        /// <summary>Regex replacement for significantly better performance</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string[] ParseCommand(string input)
        {
            int len = input.Length;

            // Fast path: no quotes — the overwhelming majority of commands
            if (input.IndexOf('"') == -1)
                return input.Split(_spaceSeparator, StringSplitOptions.RemoveEmptyEntries);

            // Slow path: quoted segments (e.g. /kick player "get out")
            var result = new List<string>(8);
            int i = 0;

            while (i < len)
            {
                while (i < len && input[i] == ' ') i++;
                if (i >= len) break;

                if (input[i] == '"')
                {
                    i++;
                    int start = i;
                    while (i < len && input[i] != '"') i++;
                    result.Add(input.Substring(start, i - start));
                    if (i < len) i++;
                }
                else
                {
                    int start = i;
                    while (i < len && input[i] != ' ') i++;
                    result.Add(input.Substring(start, i - start));
                }
            }

            return result.ToArray();
        }

        private static void Reply(IRocketPlayer player, string message, Color color)
        {
            if (player is ConsolePlayer)
            {
                Logging.Logger.Log(message, ConsoleColor.Gray);
                return;
            }

            SDG.Unturned.SteamPlayer steamPlayer = SDG.Unturned.PlayerTool.getSteamPlayer(
                new Steamworks.CSteamID(ulong.Parse(player.Id)));

            if (steamPlayer != null)
            {
                SDG.Unturned.ChatManager.serverSendMessage(
                    message,
                    color,
                    null,
                    steamPlayer
                );
            }
        }
        private bool TryResolveCommand(
            ref IRocketPlayer player,
            string command,
            out string[] commandParts,
            out IRocketCommand rocketCommand)
        {
            commandParts = null;
            rocketCommand = null;

            if (string.IsNullOrEmpty(command))
                return false;

            // Strip leading slash only when present
            if (command[0] == '/')
                command = command.Substring(1);

            commandParts = ParseCommand(command); // skip expensive Regex entirely.

            string name = commandParts[0];

            if (player == null)
                player = new ConsolePlayer();

            rocketCommand = GetCommand(name);

            if (rocketCommand == null)
            {
                Reply(player, R.Translate("command_not_found"), Color.red);
                return false;
            }

            // AllowedCaller validation only.
            // This is fundamental command validity, not permission policy.
            if (rocketCommand.AllowedCaller == AllowedCaller.Player &&
                player is ConsolePlayer)
            {
                Logging.Logger.Log("This command can't be called from console");
                return false;
            }

            if (rocketCommand.AllowedCaller == AllowedCaller.Console &&
                !(player is ConsolePlayer))
            {
                Reply(player, "This command can only be called from console", Color.red);
                return false;
            }

            return true;
        }

        private bool ValidateCommand(
            IRocketPlayer player,
            IRocketCommand rocketCommand)
        {
            if (!(player is ConsolePlayer) &&
                !R.Permissions.HasPermission(player, rocketCommand))
            {
                Reply(player, R.Translate("command_no_permission"), Color.red);
                return false;
            }

            double cooldown = GetCooldown(player, rocketCommand);

            if (cooldown > 0)
            {
                Reply(player,
                    R.Translate("command_cooldown", cooldown),
                    Color.red);

                return false;
            }

            return true;
        }

        public bool ExecuteResolved(
            IRocketPlayer player,
            string[] commandParts,
            IRocketCommand rocketCommand)
        {
            string[] parameters;

            if (commandParts.Length > 1)
            {
                int parameterCount = commandParts.Length - 1;

                parameters = new string[parameterCount];

                Array.Copy(commandParts, 1, parameters, 0, parameterCount);
            }
            else
            {
                parameters = Array.Empty<string>();
            }

            try
            {
                bool cancelCommand = false;

                ExecuteCommand snapshot = OnExecuteCommand;

                if (snapshot != null)
                {
                    Delegate[] handlers = snapshot.GetInvocationList();

                    for (int i = 0; i < handlers.Length; i++)
                    {
                        try
                        {
                            ((ExecuteCommand)handlers[i])(
                                player,
                                rocketCommand,
                                ref cancelCommand);
                        }
                        catch (Exception ex)
                        {
                            Logging.Logger.LogException(ex);
                        }
                    }
                }

                if (cancelCommand)
                    return true;

                try
                {
                    rocketCommand.Execute(player, parameters);

                    // Preserve Rocket compatibility behavior
                    if (!player.HasPermission("*"))
                        SetCooldown(player, rocketCommand);
                }
                catch (NoPermissionsForCommandException ex)
                {
                    Logging.Logger.LogWarning(ex.Message);
                }
                catch (WrongUsageOfCommandException)
                {
                    // intentionally swallowed
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.LogError(
                    "An error occured while executing " +
                    rocketCommand.Name +
                    " [" + string.Join(", ", parameters) + "]: " +
                    ex);
            }

            return true;
        }

        public bool ExecuteWithCheck(IRocketPlayer player, string command)
        {
            if (!TryResolveCommand(
                    ref player,
                    command,
                    out string[] commandParts,
                    out IRocketCommand rocketCommand))
            {
                return false;
            }

            if (!ValidateCommand(player, rocketCommand))
                return false;

            return ExecuteResolved(player, commandParts, rocketCommand);
        }

        public bool Execute(IRocketPlayer player, string command)
        {
            if (!TryResolveCommand(
                    ref player,
                    command,
                    out string[] commandParts,
                    out IRocketCommand rocketCommand))
            {
                return false;
            }

            return ExecuteResolved(player, commandParts, rocketCommand);
        }

        public void RegisterFromAssembly(Assembly assembly)
        {
            List<Type> commands = RocketHelper.GetTypesFromInterface(assembly, "IRocketCommand");
            foreach (Type commandType in commands)
            {
                if(commandType.GetConstructor(Type.EmptyTypes) != null)
                {
                    IRocketCommand command = (IRocketCommand)Activator.CreateInstance(commandType);
                    Register(command);

                    foreach(string alias in command.Aliases)
                    {
                        Register(command,alias);
                    }
                }
            }

            Type plugin = R.Plugins.GetMainTypeFromAssembly(assembly);
            if (plugin != null)
            {
                MethodInfo[] methodInfos = plugin.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (MethodInfo method in methodInfos)
                {
                    RocketCommandAttribute commandAttribute = (RocketCommandAttribute)Attribute.GetCustomAttribute(method, typeof(RocketCommandAttribute));
                    RocketCommandAliasAttribute[] commandAliasAttributes = (RocketCommandAliasAttribute[])Attribute.GetCustomAttributes(method, typeof(RocketCommandAliasAttribute));
                    RocketCommandPermissionAttribute[] commandPermissionAttributes = (RocketCommandPermissionAttribute[])Attribute.GetCustomAttributes(method, typeof(RocketCommandPermissionAttribute));

                    if (commandAttribute != null)
                    {
                        List<string> Permissions = new List<string>();
                        List<string> Aliases = new List<string>();

                        if (commandAliasAttributes != null)
                        {
                            foreach (RocketCommandAliasAttribute commandAliasAttribute in commandAliasAttributes)
                            {
                                Aliases.Add(commandAliasAttribute.Name);
                            }
                        }

                        if (commandPermissionAttributes != null)
                        {
                            foreach (RocketCommandPermissionAttribute commandPermissionAttribute in commandPermissionAttributes)
                            {
                                Aliases.Add(commandPermissionAttribute.Name);
                            }
                        }

                        IRocketCommand command = new RocketAttributeCommand(commandAttribute.Name, commandAttribute.Help, commandAttribute.Syntax, commandAttribute.AllowedCaller, Permissions, Aliases, method);
                        Register(command);
                        foreach (string alias in command.Aliases)
                        {
                            Register(command, alias);
                        }
                    }
                }
            }

            assemblies.Add(assembly);
        }

        public class RegisteredRocketCommand : IRocketCommand
        {
            public Type Type;
            public IRocketCommand Command;
            private string name;

            public RegisteredRocketCommand(string name,IRocketCommand command)
            {
                this.name = name;
                Command = command;
                Type = getCommandType(command);
            }

            public List<string> Aliases
            {
                get
                {
                    return Command.Aliases;
                }
            }

            public AllowedCaller AllowedCaller
            {
                get
                {
                    return Command.AllowedCaller;
                }
            }

            public string Help
            {
                get
                {
                    return Command.Help;
                }
            }

            public string Name
            {
                get
                {
                    return name;
                }
            }

            public List<string> Permissions
            {
                get
                {
                    return Command.Permissions;
                }
            }

            public string Syntax
            {
                get
                {
                    return Command.Syntax;
                }
            }

            public void Execute(IRocketPlayer caller, string[] command)
            {

                Command.Execute(caller, command);
            }
        }

        internal class RocketAttributeCommand : IRocketCommand
        {
            internal RocketAttributeCommand(string Name,string Help,string Syntax,AllowedCaller AllowedCaller,List<string>Permissions,List<string>Aliases,MethodInfo Method)
            {
                name = Name;
                help = Help;
                syntax = Syntax;
                permissions = Permissions;
                aliases = Aliases;
                method = Method;
                allowedCaller = AllowedCaller;
            }

            private List<string> aliases;
            public List<string> Aliases{ get { return aliases; } }

            private AllowedCaller allowedCaller;
            public AllowedCaller AllowedCaller { get { return allowedCaller; } }

            private string help;
            public string Help { get { return help; } }

            private string name;
            public string Name { get { return name; } }

            private string syntax;
            public string Syntax { get { return syntax; } }

            private List<string> permissions;
            public List<string> Permissions { get { return permissions; } }

            private MethodInfo method;
            public MethodInfo Method { get { return method; } }
            public void Execute(IRocketPlayer caller, string[] parameters)
            {
                ParameterInfo[] methodParameters = method.GetParameters();
                switch (methodParameters.Length)
                {
                    case 0:
                        method.Invoke(R.Plugins.GetPlugin(method.ReflectedType.Assembly), null);
                        break;
                    case 1:
                        if (methodParameters[0].ParameterType == typeof(IRocketPlayer))
                            method.Invoke(R.Plugins.GetPlugin(method.ReflectedType.Assembly), new object[] { caller });
                        else if (methodParameters[0].ParameterType == typeof(string[]))
                            method.Invoke(R.Plugins.GetPlugin(method.ReflectedType.Assembly), new object[] { parameters });
                        break;
                    case 2:
                        if (methodParameters[0].ParameterType == typeof(IRocketPlayer) && methodParameters[1].ParameterType == typeof(string[]))
                            method.Invoke(R.Plugins.GetPlugin(method.ReflectedType.Assembly), new object[] { caller, parameters });
                        else if (methodParameters[0].ParameterType == typeof(string[]) && methodParameters[1].ParameterType == typeof(IRocketPlayer))
                            method.Invoke(R.Plugins.GetPlugin(method.ReflectedType.Assembly), new object[] { parameters, caller });
                        break;
                }
            }
        }
    }
}