using Rocket.Core.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Xml;

namespace Rocket.Core.Steam
{
    public class Profile
    {
        public ulong SteamID64 { get; set; }
        public string SteamID { get; set; }
        public string OnlineState { get; set; }
        public string StateMessage { get; set; }
        public string PrivacyState { get; set; }
        public ushort? VisibilityState { get; set; }
        public Uri AvatarIcon { get; set; }
        public Uri AvatarMedium { get; set; }
        public Uri AvatarFull { get; set; }
        public bool? IsVacBanned { get; set; }
        public string TradeBanState { get; set; }
        public bool? IsLimitedAccount { get; set; }
        public string CustomURL { get; set; }
        public DateTime? MemberSince { get; set; }
        public double? HoursPlayedLastTwoWeeks { get; set; }
        public string Headline { get; set; }
        public string Location { get; set; }
        public string RealName { get; set; }
        public string Summary { get; set; }
        public List<MostPlayedGame> MostPlayedGames { get; set; }
        public List<Group> Groups { get; set; }

        public class MostPlayedGame
        {
            public string Name { get; set; }
            public Uri Link { get; set; }
            public Uri Icon { get; set; }
            public Uri Logo { get; set; }
            public Uri LogoSmall { get; set; }
            public double? HoursPlayed { get; set; }
            public double? HoursOnRecord { get; set; }
        }

        public class Group
        {
            public ulong? SteamID64 { get; set; }
            public bool IsPrimary { get; set; }
            public string Name { get; set; }
            public string URL { get; set; }
            public Uri AvatarIcon { get; set; }
            public Uri AvatarMedium { get; set; }
            public Uri AvatarFull { get; set; }
            public string Headline { get; set; }
            public string Summary { get; set; }
            public uint? MemberCount { get; set; }
            public uint? MembersInGame { get; set; }
            public uint? MembersInChat { get; set; }
            public uint? MembersOnline { get; set; }
        }

        public Profile(ulong steamID64)
        {
            SteamID64 = steamID64;
            Reload();
        }

        private class TimedWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                req.Timeout = 5000; // don't hang the calling thread indefinitely
                return req;
            }
        }

        private static readonly CultureInfo _usCulture = new CultureInfo("en-US", false);

        public void Reload()
        {
            string field = "unknown";
            try
            {
                string xml;
                using (var wc = new TimedWebClient())
                    xml = wc.DownloadString(
                        "http://steamcommunity.com/profiles/" + SteamID64 + "?xml=1");

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                XmlElement profile = doc["profile"];
                if (profile == null)
                    throw new Exception("Steam API returned no <profile> element.");

                SteamID = profile["steamID"]?.ParseString(); field = "SteamID";
                OnlineState = profile["onlineState"]?.ParseString(); field = "OnlineState";
                StateMessage = profile["stateMessage"]?.ParseString(); field = "StateMessage";
                PrivacyState = profile["privacyState"]?.ParseString(); field = "PrivacyState";
                VisibilityState = profile["visibilityState"]?.ParseUInt16(); field = "VisibilityState";
                AvatarIcon = profile["avatarIcon"]?.ParseUri(); field = "AvatarIcon";
                AvatarMedium = profile["avatarMedium"]?.ParseUri(); field = "AvatarMedium";
                AvatarFull = profile["avatarFull"]?.ParseUri(); field = "AvatarFull";
                IsVacBanned = profile["vacBanned"]?.ParseBool(); field = "IsVacBanned";
                TradeBanState = profile["tradeBanState"]?.ParseString(); field = "TradeBanState";
                IsLimitedAccount = profile["isLimitedAccount"]?.ParseBool(); field = "IsLimitedAccount";
                CustomURL = profile["customURL"]?.ParseString(); field = "CustomURL";
                MemberSince = profile["memberSince"]?.ParseDateTime(_usCulture); field = "MemberSince";
                HoursPlayedLastTwoWeeks = profile["hoursPlayed2Wk"]?.ParseDouble(); field = "HoursPlayedLastTwoWeeks";
                Headline = profile["headline"]?.ParseString(); field = "Headline";
                Location = profile["location"]?.ParseString(); field = "Location";
                RealName = profile["realname"]?.ParseString(); field = "RealName";
                Summary = profile["summary"]?.ParseString(); field = "Summary";

                XmlElement mostPlayedGamesNode = profile["mostPlayedGames"];
                if (mostPlayedGamesNode != null)
                {
                    XmlNodeList gameNodes = mostPlayedGamesNode.ChildNodes;
                    MostPlayedGames = new List<MostPlayedGame>(gameNodes.Count);
                    field = "MostPlayedGames";
                    for (int i = 0; i < gameNodes.Count; i++)
                    {
                        var g = gameNodes[i] as XmlElement;
                        if (g == null) continue; // skip text/comment nodes

                        MostPlayedGames.Add(new MostPlayedGame
                        {
                            Name = g["gameName"]?.ParseString(),
                            Link = g["gameLink"]?.ParseUri(),
                            Icon = g["gameIcon"]?.ParseUri(),
                            Logo = g["gameLogo"]?.ParseUri(),
                            LogoSmall = g["gameLogoSmall"]?.ParseUri(),
                            HoursPlayed = g["hoursPlayed"]?.ParseDouble(),
                            HoursOnRecord = g["hoursOnRecord"]?.ParseDouble()
                        });
                    }
                }

                XmlElement groupsNode = profile["groups"];
                if (groupsNode != null)
                {
                    XmlNodeList groupNodes = groupsNode.ChildNodes;
                    Groups = new List<Group>(groupNodes.Count);
                    field = "Groups";
                    for (int i = 0; i < groupNodes.Count; i++)
                    {
                        var g = groupNodes[i] as XmlElement;
                        if (g == null) continue;

                        Groups.Add(new Group
                        {
                            IsPrimary = g.Attributes["isPrimary"]?.InnerText == "1",
                            SteamID64 = g["groupID64"]?.ParseUInt64(),
                            Name = g["groupName"]?.ParseString(),
                            URL = g["groupURL"]?.ParseString(),
                            Headline = g["headline"]?.ParseString(),
                            Summary = g["summary"]?.ParseString(),
                            AvatarIcon = g["avatarIcon"]?.ParseUri(),
                            AvatarMedium = g["avatarMedium"]?.ParseUri(),
                            AvatarFull = g["avatarFull"]?.ParseUri(),
                            MemberCount = g["memberCount"]?.ParseUInt32(),
                            MembersInChat = g["membersInChat"]?.ParseUInt32(),
                            MembersInGame = g["membersInGame"]?.ParseUInt32(),
                            MembersOnline = g["membersOnline"]?.ParseUInt32()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error reading Steam Profile, Field: " + field);
            }
        }
    }
    public static class XmlElementExtensions
    {
        public static string ParseString(this XmlElement element)
        {
            return element.InnerText;
        }


        public static DateTime? ParseDateTime(this XmlElement element, CultureInfo cultureInfo)
        {
            try
            {
                return element == null ? null : (DateTime?)DateTime.Parse(element.InnerText.Replace("st", "").Replace("nd", "").Replace("rd", "").Replace("th", ""), cultureInfo);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static double? ParseDouble(this XmlElement element)
        {
            try
            {
                return element == null ? null : (double?)double.Parse(element.InnerText);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static ushort? ParseUInt16(this XmlElement element)
        {
            try
            {
                return element == null ? null : (ushort?)ushort.Parse(element.InnerText);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static uint? ParseUInt32(this XmlElement element)
        {
            try
            {
                return element == null ? null : (uint?)uint.Parse(element.InnerText);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static ulong? ParseUInt64(this XmlElement element)
        {
            try
            {
                return element == null ? null : (ulong?)ulong.Parse(element.InnerText);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool? ParseBool(this XmlElement element)
        {
            try
            {
                return element == null ? null : (bool?)(element.InnerText == "1");
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static Uri ParseUri(this XmlElement element)
        {
            try
            {
                return element == null ? null : new Uri(element.InnerText);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}