using System;
using System.Collections.Generic;
using UnityEngine;

namespace SportsTour
{
    [Serializable]
    public class HotspotLink
    {
        [Tooltip("Id of the section this arrow goes to")]
        public string to;
        [Tooltip("Direction in degrees: 0 = ahead at start, 90 = right, -90 = left, 180 = behind")]
        public float yaw;
        [Tooltip("Up/down in degrees: 0 = eye level, negative = towards the ground")]
        public float pitch = -8f;

        public HotspotLink(string to, float yaw, float pitch = -8f) { this.to = to; this.yaw = yaw; this.pitch = pitch; }
    }

    [Serializable]
    public class TourSection
    {
        public string id;
        [Tooltip("Shown in the menu and in the 'YOU ARE AT' label")]
        public string name;
        [Tooltip("File name inside Assets/StreamingAssets/Videos (packed into the app)")]
        public string videoFile;
        public Color color = Color.white;
        [Tooltip("Hotspot arrows shown inside this video. In Play mode press H while looking at a spot to get its yaw/pitch.")]
        public List<HotspotLink> links = new List<HotspotLink>();

        public TourSection(string id, string name, string videoFile, string hex, params HotspotLink[] links)
        {
            this.id = id; this.name = name; this.videoFile = videoFile;
            ColorUtility.TryParseHtmlString(hex, out color);
            this.links.AddRange(links);
        }

        // Same tour as the web version: entrance links to everything, each court links back + to its neighbours.
        public static List<TourSection> Defaults()
        {
            HotspotLink L(string to, float yaw) => new HotspotLink(to, yaw);
            HotspotLink[] Ring(string prev, string next) => new[] { L("entrance", 180), L(prev, -60), L(next, 60) };
            return new List<TourSection>
            {
                new TourSection("entrance",   "Sports Complex Entrance", "Entrance.mp4",   "#7c5cff",
                    L("athletics", 0), L("basketball", 45), L("cricket", 90), L("football", 135),
                    L("tennis", 180), L("pickleball", -135), L("volleyball", -90), L("skating", -45)),
                new TourSection("athletics",  "Athletics Track",  "Athletics.mp4",  "#22c55e", Ring("skating", "basketball")),
                new TourSection("basketball", "Basketball Court", "Basketball.mp4", "#f97316", Ring("athletics", "cricket")),
                new TourSection("cricket",    "Cricket Ground",   "Cricket.mp4",    "#84cc16", Ring("basketball", "football")),
                new TourSection("football",   "Football Field",   "Football.mp4",   "#10b981", Ring("cricket", "tennis")),
                new TourSection("tennis",     "Tennis Court",     "Tennis.mp4",     "#eab308", Ring("football", "pickleball")),
                new TourSection("pickleball", "Pickleball Court", "PickleBall.mp4", "#06b6d4", Ring("tennis", "volleyball")),
                new TourSection("volleyball", "Volleyball Court", "Volleyball.mp4", "#3b82f6", Ring("pickleball", "skating")),
                new TourSection("skating",    "Skating Rink",     "Skating.mp4",    "#ec4899", Ring("volleyball", "athletics")),
            };
        }
    }
}
