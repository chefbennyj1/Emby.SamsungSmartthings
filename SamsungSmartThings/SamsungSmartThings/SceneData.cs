using System;
using System.Collections.Generic;

namespace SamsungSmartThings.SamsungSmartThings
{
    public class Item
                                         {
        public string sceneId            { get; set; }
        public string sceneName          { get; set; }
        public string sceneIcon          { get; set; }
        public string sceneColor         { get; set; }
        public string locationId         { get; set; }
        public string createdBy          { get; set; }
        public DateTime createdDate      { get; set; }
        public DateTime lastUpdatedDate  { get; set; }
        public DateTime lastExecutedDate { get; set; }
        public bool editable             { get; set; }
        public string apiVersion         { get; set; }
    }

    public class Next
    {
        public string href               { get; set; }
    }

    public class Previous
    {
        public string href               { get; set; }
    }

    public class Links
    {
        public Next next                 { get; set; }
        public Previous previous         { get; set; }
    }

    public class SceneData
    {
        public List<Item> items          { get; set; }
        public Links _links              { get; set; }
    }
}
