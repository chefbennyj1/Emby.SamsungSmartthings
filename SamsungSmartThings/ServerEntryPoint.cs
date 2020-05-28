using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
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
        private static IHttpClient Client             { get; set; }
        private static ServerEntryPoint Instance      { get; set; }
        private static ISessionManager SessionManager { get; set; }
        // Length of  video backdrop /or Emby intro in ticks. We need to ignore this.
        private const long IntroOrVideoBackDrop = 3000000000L;

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

        private static List<string> CreditSessions = new List<string>();

        private void PlaybackCredits(PlaybackProgressEventArgs e, SavedProfile profile, PluginConfiguration config)
        {
            if (CreditSessions.Exists(s => s.Equals(e.Session.Id))) return; //We've already triggered the event, it's in the list - move on

            CreditSessions.Add(e.Session.Id); //Add the session ID to the list so this event doesn't trigger again
            logger.Info($"Samsung Smart Things Reports trigger Credit Scene on {e.DeviceName}"); //Log that shit.

            RunScene(profile.MediaItemCredits, config, "CreditScene");

        }
        

        private static readonly List<string> PausedSessionsIds = new List<string>();
        private static bool IgnoreEvents;
        private void PlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            var config = Plugin.Instance.Configuration;
            SavedProfile profile = null;

            if (config.SaveSmartThingsProfiles.Exists(p => p.DeviceName.Equals(e.Session.DeviceName)))
            {
                profile = config.SaveSmartThingsProfiles.FirstOrDefault(p => p.DeviceName.Equals(e.DeviceName) && p.AppName.Equals(e.Session.Client));
            }
            else
            {
                return;
            }
            
            // ReSharper disable once ComplexConditionExpression
            if (e.MediaInfo.Type.Equals("Movie") && e.Session.PlayState.PositionTicks >= (e.Item.RunTimeTicks - (profile.MediaItemCreditLength * 10000000)))
            {
                if (!ReferenceEquals(null, profile.MediaItemCredits))
                {
                    PlaybackCredits(e, profile, config);
                }
            }

            if (IgnoreEvents)
            {
                return;
            }

            if (PausedSessionsIds.Contains(e.Session.Id))
            {
                if (!e.IsPaused || !e.Session.PlayState.IsPaused)
                {
                    IgnoreEvents = true;
                    PausedSessionsIds.RemoveAll(s => s.Equals(e.Session.Id));
                    PlaybackUnPaused(e, config, profile);
                    IgnoreEvents = false;
                }
                return;
            }

            if(!PausedSessionsIds.Contains(e.Session.Id))
            {
                if (e.IsPaused || e.Session.PlayState.IsPaused)
                {
                    IgnoreEvents = true;
                    PausedSessionsIds.Add(e.Session.Id);
                    PlaybackPaused(e, config, profile);
                    IgnoreEvents = false;
                }
            }
                    
        }

        // ReSharper disable once TooManyArguments
        private void PlaybackUnPaused(PlaybackProgressEventArgs e, PluginConfiguration config, SavedProfile profile)
        {
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

            logger.Info($"Samsung Smart Things Reports {e.GetType().Name} will trigger Playback UnPaused Scene for {e.DeviceName}");

            RunScene(sceneName, config, "UnPause");

        }

        // ReSharper disable once TooManyArguments
        private void PlaybackPaused(PlaybackProgressEventArgs e, PluginConfiguration config, SavedProfile profile)
        {
            logger.Info("Samsung Smart Things Reports Playback Paused...");

            logger.Info($"Samsung Smart Things Found Session Device: { profile.DeviceName }");

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

            logger.Info($"Samsung Smart Things will trigger Paused: { profile.DeviceName }");
            RunScene(sceneName, config);

        }

        private void PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            logger.Info("Samsung Smart Things Reports Playback Stopped");

            var config = Plugin.Instance.Configuration;
            
            if (e.IsPaused) return;
            
            //We check here if a profile exists or return
            if (!config.SaveSmartThingsProfiles.Exists(p => p.DeviceName.Equals(e.DeviceName) &&
                                                         p.AppName.Equals(e.ClientName))) return;

            //The item was in a paused state when the user stopped it, clean up the paused session list.
            if (PausedSessionsIds.Exists(s => s.Equals(e.Session.Id))) PausedSessionsIds.RemoveAll(s => s.Equals(e.Session.Id));

            //The item might appear in the credit session list remove it if it does.
            if (CreditSessions.Exists(s => s.Equals(e.Session.Id))) CreditSessions.RemoveAll(s => s.Equals(e.Session.Id));

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

            RunScene (sceneName, config, "PlaybackStopped");

        }

        private void PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            // ReSharper disable once ComplexConditionExpression
            if (e.MediaInfo.RunTimeTicks != null && (e.Item.MediaType == MediaType.Video && e.MediaInfo.RunTimeTicks.Value < IntroOrVideoBackDrop))
            {
                return;
            }

            if (e.IsPaused) return;

            var config = Plugin.Instance.Configuration;
            
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

            RunScene(sceneName, config, "PlaybackStarted");

        }
        
        private static bool ScheduleAllowScene(SavedProfile profile)
        {
            if (string.IsNullOrEmpty(profile.Schedule)) return true;

            return (DateTime.Now.TimeOfDay >= TimeSpan.Parse(profile.Schedule + ":00")) &&
                   (DateTime.Now <= DateTime.Now.Date.AddDays(1).AddHours(4));
        }

        private void RunScene(string sceneId, PluginConfiguration config, string eventName = "")
        {
            logger.Info($"{eventName} will run {sceneId}");
            var sceneUrl = "https://api.smartthings.com/v1/scenes/" + sceneId + "/execute";
            
            try
            {
                var req = HttpWebRequest.Create(sceneUrl);
                req.Method = "POST";
                req.Headers.Add("Authorization", "Bearer " + config.PersonalAccessToken);
                using (WebResponse response = req.GetResponse())
                {
                    using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        var json =  streamReader.ReadToEnd();
                    }
                }

                logger.Info($"Scene Successfully executed: {eventName}");

            }
            catch (Exception ex)
            {
                logger.Error("SMART THINGS - " + ex.Message);
            }
            
            /*
            var request =  WebRequest.Create(sceneUrl);
            request.Headers["Authorization"] = "Bearer " + config.PersonalAccessToken;
            request.Method = "POST";
            request.ContentLength = 0;
            request.ContentType = "application/x-www-form-urlencoded";
            using (Stream dataStream = request.GetRequestStream())
            {
                dataStream.Write(new byte[0], 0, 1);
            }
            */

            /*
            using (var client = new HttpClient())
            {
                
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", config.PersonalAccessToken);
                await client.PostAsync(sceneUrl, new MultipartContent("application/json"), CancellationToken.None);
            }
            */
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
