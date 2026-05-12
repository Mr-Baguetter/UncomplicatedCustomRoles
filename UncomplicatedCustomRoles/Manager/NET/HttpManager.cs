/*
 * This file is a part of the UncomplicatedCustomRoles project.
 * 
 * Copyright (c) 2023-present FoxWorn3365 (Federico Cosma) <me@fcosma.it>
 * 
 * This file is licensed under the GNU Affero General Public License v3.0.
 * You should have received a copy of the AGPL license along with this file.
 * If not, see <https://www.gnu.org/licenses/>.
 */

using MEC;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using UncomplicatedCustomRoles.API.Struct;
using UncomplicatedCustomRoles.Extensions;
using UncomplicatedCustomRoles.API.Features.Messages;
using System.Text.Json.Serialization;

namespace UncomplicatedCustomRoles.Manager.NET
{
#pragma warning disable IDE1006

    internal class HttpManager
    {
        // This is assuming that credit tags are still following the same format.
        public class CreditTag
        {
            public CreditTag(string role, string color, bool over)
            {
                Role = role;
                Color = color;
                Override = over;
            }

            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("color")]
            public string Color { get; set; } = string.Empty;

            [JsonPropertyName("override")]
            public bool Override { get; set; }

            [JsonPropertyName("job")]
            public bool Job { get; set; }
        }

        /// <summary>
        /// Gets the CreditTag storage for the plugin, downloaded from our central server
        /// </summary>
        public Dictionary<string, CreditTag> CreditTags
        {
            get
            {
                if (field == null || field.IsEmpty())
                    LoadCreditTags();

                return field;
            }

            internal set;
        } = [];

        /// <summary>
        /// Gets the <see cref="CoroutineHandle"/> of the presence coroutine.
        /// </summary>
        public CoroutineHandle PresenceCoroutine { get; internal set; }

        /// <summary>
        /// Gets if the feature can be activated - missing library
        /// </summary>
        public bool IsAllowed { get; internal set; } = true;

        /// <summary>
        /// Gets the prefix of the plugin for our APIs
        /// </summary>
        public string Prefix { get; }

        /// <summary>
        /// Gets the <see cref="HttpClient"/> public instance
        /// </summary>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// Gets the UCS APIs endpoint
        /// </summary>
        public string Endpoint { get; } = "https://api.ucserver.it/v3/plugin";

        /// <summary>
        /// Gets the role of the given player (as steamid@64) inside UCR
        /// </summary>
        public List<string> IsJobRole => CreditTags.Where(x => x.Value.Job).Select(x => x.Key).ToList();

        /// <summary>
        /// Gets the latest <see cref="Version"/> of the plugin, loaded by the UCS cloud
        /// </summary>
        public Version LatestVersion {
            get
            {
                if (field is null)
                    LoadLatestVersion();

                return field;
            }

            set;
        }

        /// <summary>
        /// Create a new istance of the HttpManager
        /// </summary>
        /// <param name="prefix"></param>
        public HttpManager(string prefix)
        {
            Prefix = prefix;
            RegisterEvents();
            HttpClient = new();
            Task.Run(LoadCreditTags);
        }

        internal void RegisterEvents()
        {
            PlayerEvents.Joined += OnVerified;
        }

        internal void UnregisterEvents()
        {
            PlayerEvents.Joined -= OnVerified;
        }

        public void OnVerified(PlayerJoinedEventArgs ev) => ApplyCreditTag(ev.Player);

        public string AddServerOwner(Player player, string discordId)
        {
            return HttpQuery.Post("https://api.ucserver.it/v3/owners", JsonSerializer.Serialize(new OwnerMessage(player, discordId)), "application/json");
        }

        public void LoadLatestVersion()
        {
            string Version = HttpQuery.Get($"{Endpoint}/{Prefix}/versions/latest@text/plain");

            if (!string.IsNullOrEmpty(Version) && Version.Contains("."))
            {
                LatestVersion = new(Version);
            }
            else
                LatestVersion = new();
        }

        public void LoadCreditTags()
        {
            try
            {
                CreditTags = JsonSerializer.Deserialize<Dictionary<string, CreditTag>>(HttpQuery.Get($"https://api.ucserver.it/credits.json"));

                if (CreditTags is null || CreditTags.IsEmpty())
                {
                    LogManager.Warn("Failed to connect to the UCS Central Server to get the credit tags informations!");
                    return;
                }
            }
            catch (Exception e)
            {
                LogManager.Error("An error occurred while loading the credit tags from the UCS Central Server!");
                LogManager.Debug($"Failed to act HttpManager::LoadCreditTags() - {e.GetType().FullName}: {e.Message}\n{e.StackTrace}");
            }
        }

        public CreditTag GetCreditTag(Player player)
        {
            if (CreditTags.TryGetValue(player.UserId, out var tag))
                return tag;

            return null;
        }

        public void ApplyCreditTag(Player player)
        {
            if (!Plugin.Instance.Config.EnableCreditTags)
                return;
            
            CreditTag tag = GetCreditTag(player);

            if (player.UserGroup != null && !tag.Override)
                return;

            if (tag.Role == player.GroupName && tag.Color == player.GroupColor)
                return;

            player.GroupName = tag.Role;
            player.GroupColor = tag.Color;
            LogManager.Debug($"Applied credit tag for player {player.Nickname} ({player.UserId}) - Role: {tag.Role}, Color: {tag.Color}");
        }

        public bool IsLatestVersion(out Version latest)
        {
            latest = LatestVersion;
            if (latest.CompareTo(Plugin.Instance.Version) > 0)
                return false;

            return true;

        }

        public bool IsLatestVersion()
        {
            if (LatestVersion.CompareTo(Plugin.Instance.Version) > 0)
                return false;

            return true;
        }

        internal HttpStatusCode ShareLogs(string data, out string content)
        {
            content = HttpQuery.Post($"{Endpoint}/{Prefix}/logs", JsonSerializer.Serialize(new ShareLogMessage(data)), "application/json");
            return content.GetStatusCode(out _);
        }

#nullable enable
        internal string VersionInfo()
        {
            return HttpQuery.Get($"{Endpoint}/{Prefix}/versions/{Plugin.Instance.Version.ToString(4)}");
        }
    }
}