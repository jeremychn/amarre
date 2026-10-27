export enum RemoteControlQualityMode {
    /** Auto-adapts quality based on network conditions (latency and frame delivery).
     * This is the default mode. */
    Auto = 0,

    /** Uses lower image quality to reduce bandwidth usage and improve performance
     * on slow or congested networks. */
    Performance = 1,

    /** Uses the highest image quality at the cost of increased bandwidth.
     * Best for high-speed local networks where quality is the priority. */
    Quality = 2
}
