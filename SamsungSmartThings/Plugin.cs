using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using SamsungSmartThings.Configuration;

namespace SamsungSmartThings
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public static Plugin Instance { get; set; }
        public ImageFormat ThumbImageFormat => ImageFormat.Jpg;

        private readonly Guid _id = new Guid("6952341D-5579-41D7-A771-703484AECAD9");
        public override Guid Id => _id;

        public override string Name => "Samsung Smart Things";


        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
        }

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths,
            xmlSerializer)
        {
            Instance = this;
        }

        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {
            new PluginPageInfo
            {
                Name = "SmartThingsPage",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.SmartThingsPage.html"
            },
            new PluginPageInfo
            {
                Name = "SmartThingsPageJS",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.SmartThingsPage.js"
            }
        };
    }
}
   
