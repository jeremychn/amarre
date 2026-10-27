export var RemoteControlQualityMode;
(function (RemoteControlQualityMode) {
    /** Auto-adapts quality based on network conditions (latency and frame delivery).
     * This is the default mode. */
    RemoteControlQualityMode[RemoteControlQualityMode["Auto"] = 0] = "Auto";
    /** Uses lower image quality to reduce bandwidth usage and improve performance
     * on slow or congested networks. */
    RemoteControlQualityMode[RemoteControlQualityMode["Performance"] = 1] = "Performance";
    /** Uses the highest image quality at the cost of increased bandwidth.
     * Best for high-speed local networks where quality is the priority. */
    RemoteControlQualityMode[RemoteControlQualityMode["Quality"] = 2] = "Quality";
})(RemoteControlQualityMode || (RemoteControlQualityMode = {}));
//# sourceMappingURL=RemoteControlQualityMode.js.map