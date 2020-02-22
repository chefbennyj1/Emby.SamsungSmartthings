using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using SamsungSmartThings.Configuration;

namespace SamsungSmartThings
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private ILogger logger                        { get; set; }
        private ILogManager LogManager                { get; set; }
        private static IJsonSerializer JsonSerializer { get; set; }
        public static IHttpClient Client             { get; set; }
        private static ServerEntryPoint Instance      { get; set; }
        private static ISessionManager SessionManager { get; set; }
        

        // ReSharper disable once TooManyDependencies
        public ServerEntryPoint(IJsonSerializer jsonSerializer, IHttpClient client, ISessionManager sessionManager, ILogManager logManager)
        {
            JsonSerializer = jsonSerializer;
            Client         = client;
            Instance       = this;
            SessionManager = sessionManager;
            LogManager     = logManager;
            logger         = LogManager.GetLogger(Plugin.Instance.Name);
        }


        public void Dispose()
        {
            SessionManager.PlaybackStart    -= PlaybackStart;
            SessionManager.PlaybackStopped  -= PlaybackStopped;
            SessionManager.PlaybackProgress -= PlaybackProgress;
        }

        public void Run()
        {
            SessionManager.PlaybackStart    += PlaybackStart;
            SessionManager.PlaybackStopped  += PlaybackStopped;
            SessionManager.PlaybackProgress += PlaybackProgress;
        }

        private List<string> PausedSessionsIds = new List<string>();

        private void PlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            var config = Plugin.Instance.Configuration;
            
            
            //No paused Session and no flagged sessions paused, move on
            // ReSharper disable once ComplexConditionExpression
            if (!SessionManager.Sessions.Any(s => s.PlayState.IsPaused) && !PausedSessionsIds.Any()) return;
            
            switch (e.Session.PlayState.IsPaused)
            {
                case true:
                    // We've already flagged this session, move on
                    if (PausedSessionsIds.Exists(s => s.Equals(e.Session.Id))) return;
                    //We don't have a profile for this paused session device, move on
                    if (!config.SaveSmartThingsProfiles.Exists(p => p.DeviceName.Equals(e.Session.DeviceName))) return;

                    PausedSessionsIds.Add(e.Session.Id);

                    PlaybackPaused(e, config, e.Session,
                            config.SaveSmartThingsProfiles.FirstOrDefault(p =>
                                p.DeviceName.Equals(e.Session.DeviceName)));

                    break;

                case false:

                    if (PausedSessionsIds.Exists(s => s.Equals(e.Session.Id)))
                    {
                        PlaybackUnPaused(e, config, config.SaveSmartThingsProfiles.FirstOrDefault(p => p.DeviceName.Equals(e.Session.DeviceName)));
                        PausedSessionsIds.RemoveAll(s => s.Equals(e.Session.Id));
                    }

                    break;
            }
           
        }

        // ReSharper disable once TooManyArguments
        private void PlaybackUnPaused(PlaybackProgressEventArgs e, PluginConfiguration config, SavedProfile profile)
        {
            if (config.HubIpAddress == null) return;

            logger.Info("Samsung Smart Things Reports Playback UnPaused...");

            logger.Info("Samsung Smart Things Found Profile Device: " + profile.DeviceName);

            if (!ScheduleAllowScene(profile))
            {
                logger.Info("Samsung Smart Things profile not allowed to run at this time: " + profile.DeviceName);
                return;
            }

            var sceneName = string.Empty;
            switch (e.MediaInfo.Type)
            {
                case "Movie":
                    sceneName = profile.MoviesPlaybackUnPaused;
                    break;
                case "TvChannel":
                    sceneName = profile.LiveTvPlaybackUnPaused;
                    break;
                case "Series":
                    sceneName = profile.TvPlaybackUnPaused;
                    break;
                case "Season":
                    sceneName = profile.TvPlaybackUnPaused;
                    break;
                case "Episode":
                    sceneName = profile.TvPlaybackUnPaused;
                    break;
            }

            logger.Info($"Samsung Smart Things Reports {e.MediaInfo.Type} will trigger Playback UnPaused Scene for {e.DeviceName}");

            RunScene(sceneName, config);

        }

        // ReSharper disable once TooManyArguments
        private void PlaybackPaused(PlaybackProgressEventArgs e, PluginConfiguration config, SessionInfo session, SavedProfile profile)
        {
            if (config.HubIpAddress == null) return;

            logger.Info("Samsung Smart Things Reports Playback Paused...");

            logger.Info($"Samsung Smart Things Found Session Device: { session.DeviceName }");

            if (!ScheduleAllowScene(profile))
            {
                logger.Info($"Samsung Smart Things profile not allowed to run at this time: { profile.DeviceName }");
                return;
            }

            var sceneName = string.Empty;

            switch (e.MediaInfo.Type)
            {
                case "Movie":
                    sceneName = profile.MoviesPlaybackPaused;
                    break;
                case "TvChannel":
                    sceneName = profile.LiveTvPlaybackPaused;
                    break;
                case "Series":
                    sceneName = profile.TvPlaybackPaused;
                    break;
                case "Season":
                    sceneName = profile.TvPlaybackPaused;
                    break;
                case "Episode":
                    sceneName = profile.TvPlaybackPaused;
                    break;
            }

            RunScene(sceneName, config);


        }

        private void PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            logger.Info("Samsung Smart Things Reports Playback Stopped");

            var config = Plugin.Instance.Configuration;

            if (config.HubIpAddress == null) return;

            if (e.IsPaused) return;

            //We check here if a profile exists or return
            if (!config.SaveSmartThingsProfiles.Exists(p => p.DeviceName.Equals(e.DeviceName) &&
                                                         p.AppName.Equals(e.ClientName))) return;

            //The item was in a paused state when the user stopped it, clean up the paused session list.
            if (PausedSessionsIds.Exists(s => s.Equals(e.Session.Id))) PausedSessionsIds.RemoveAll(s => s.Equals(e.Session.Id));

           

            //We can assume this will not be null, even though he have to assert it is not null below "profile?.{property}"
            var profile = config.SaveSmartThingsProfiles.FirstOrDefault(p => p.DeviceName.Equals(e.DeviceName) &&
                                                                          p.AppName.Equals(e.ClientName));

            logger.Info($"Samsung Smart Things Found Profile Device: { e.DeviceName } ");

            if (!ScheduleAllowScene(profile))
            {
                logger.Info($"Samsung Smart Things profile not allowed to run at this time: { profile?.DeviceName }");
                return;
            }

            var sceneName = string.Empty;
            switch (e.MediaInfo.Type)
            {
                case "Movie":
                    sceneName = profile?.MoviesPlaybackStopped;
                    break;
                case "TvChannel":
                    sceneName = profile?.LiveTvPlaybackStopped;
                    break;
                case "Series":
                    sceneName = profile?.TvPlaybackStopped;
                    break;
                case "Season":
                    sceneName = profile?.TvPlaybackStopped;
                    break;
                case "Episode":
                    sceneName = profile?.TvPlaybackStopped;
                    break;
            }

            logger.Info("Samsung Smart Things Reports " + e.MediaInfo.Type + " will trigger Playback Stopped Scene on " + e.DeviceName);

            RunScene (sceneName, config);

        }

        private void PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            var config = Plugin.Instance.Configuration;
            if (config.HubIpAddress == null) return;

            //No profile, move on
            if (!config.SaveSmartThingsProfiles.Exists(p => p.DeviceName.Equals(e.DeviceName) && p.AppName.Equals(e.ClientName))) return;

            logger.Info("Samsung Smart Things Reports Playback Started");

            var profile = config.SaveSmartThingsProfiles.FirstOrDefault(p => p.DeviceName.Equals(e.DeviceName) &&
                                                                          p.AppName.Equals(e.ClientName));

            logger.Info($"Samsung Smart Things Found Profile Device: { e.DeviceName }");

            if (!ScheduleAllowScene(profile))
            {
                logger.Info($"Samsung Smart Things profile not allowed to run at this time: { profile?.DeviceName }");
                return;
            }

            var sceneName = string.Empty;

            switch (e.MediaInfo.Type)
            {
                case "Movie":
                    sceneName = profile?.MoviesPlaybackStarted;
                    break;
                case "TvChannel":
                    sceneName = profile?.LiveTvPlaybackStarted;
                    break;
                case "Series":
                    sceneName = profile?.TvPlaybackStarted;
                    break;
                case "Season":
                    sceneName = profile?.TvPlaybackStarted;
                    break;
                case "Episode":
                    sceneName = profile?.TvPlaybackStarted;
                    break;
            }

            logger.Info($"Samsung Smart Things Reports { e.MediaInfo.Type } will trigger Playback Started Scene on { e.DeviceName }");

            RunScene(sceneName, config);

        }

        
        
        private static bool ScheduleAllowScene(SavedProfile profile)
        {
            if (string.IsNullOrEmpty(profile.Schedule)) return true;

            return (DateTime.Now.TimeOfDay >= TimeSpan.Parse(profile.Schedule + ":00")) &&
                   (DateTime.Now <= DateTime.Now.Date.AddDays(1).AddHours(4));
        }

        private async void RunScene(string sceneId, PluginConfiguration config)
        {
            var sceneUrl = "https://" + $"api.smartthings.com/v1/scenes/{sceneId}/execute";
            using (var client = new HttpClient())
            {
                
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", config.PersonalAccessToken);
                await client.PostAsync(sceneUrl, new MultipartContent("application/json"), CancellationToken.None);
            }

            /*
            try
            {
                var sceneUrl = "https://" + $"api.smartthings.com/v1/scenes/{sceneId}/execute";

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(sceneUrl);
                httpWebRequest.Method = "POST";
                httpWebRequest.Headers.Add("Authorization", "Bearer " + Plugin.Instance.Configuration.PersonalAccessToken);

                using (var response = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK) return;

                    using (var receiveStream = response.GetResponseStream())
                    {
                        if (receiveStream == null) return;

                        using (var sr = new StreamReader(receiveStream,
                            Encoding.GetEncoding(response.CharacterSet ?? throw new InvalidOperationException())))
                        {
                            var data = sr.ReadToEnd();

                            var results = JsonSerializer.DeserializeFromString<SceneResponse>(data);
                        }
                    }
                }


            }
            catch { }
            */
        }

        public class SceneResponse
        {
            public string status { get; set; }
        }
    }
}
