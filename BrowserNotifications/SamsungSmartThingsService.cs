using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Devices;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace SamsungSmartThings
{
    //ab88448e-abaf-4dce-be0c-7c56f54b7d24

    [Authenticated(Roles = "Admin")]
    [Route("/EmbyDeviceList", "GET", Summary = "Sorted Emby Device List End Point")]
    public class EmbyDeviceList : IReturn<string>
    {
        public string Devices { get; set; }
    }
    
    public class SamsungSmartThingsService : IService
    {

        private IJsonSerializer jsonSerializer { get; set; }
        private IHttpClient httpClient         { get; set; }
        private IDeviceManager deviceManager   { get; set; }
        private readonly ILogger logger;


        // ReSharper disable once TooManyDependencies
        public SamsungSmartThingsService(ILogManager logManager, IHttpClient httpClient, IJsonSerializer jsonSerializer, IDeviceManager deviceManager)
        {
            logger = logManager.GetLogger(GetType().Name);

            this.httpClient     = httpClient;
            this.jsonSerializer = jsonSerializer;
            this.deviceManager  = deviceManager;
        }

        [Route("/GetSmartThingsScenes", "GET")]
        public class GetScenes : IReturn<string>
        {
            public string Scenes { get; set; }
        }

        public string Get(GetScenes request)
        {
            var config = Plugin.Instance.Configuration;

            try
            {
                var req = HttpWebRequest.Create("https://api.smartthings.com/v1/scenes");
                req.Method = "GET";
                req.Headers.Add("Authorization", "Bearer " + config.PersonalAccessToken);
                using (WebResponse response = req.GetResponse())
                {
                    using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        return streamReader.ReadToEnd();
                    }
                }

            }
            catch (Exception ex)
            {
                logger.Error("SMART THINGS - " + ex.Message);
            }

            return string.Empty;

        }

        public string Get(EmbyDeviceList request)
        {
            var deviceInfo = deviceManager.GetDevices(new DeviceQuery());

            var deviceList = new List<DeviceInfo>();

            foreach (var device in deviceInfo.Items)
            {
                if (!deviceList.Exists(x =>
                    string.Equals(x.Name, device.Name, StringComparison.CurrentCultureIgnoreCase) &&
                    string.Equals(x.AppName, device.AppName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    deviceList.Add(device);
                }
            }

            return jsonSerializer.SerializeToString(deviceList);

        }

       

    }
}
