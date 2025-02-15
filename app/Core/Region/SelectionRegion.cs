using System;
using System.Drawing;

namespace GameTranslationOverlay.Core.Region
{
    /// <summary>
    /// Represents a selected region for OCR processing
    /// </summary>
    public class SelectionRegion
    {
        // Region properties
        public Rectangle Bounds { get; private set; }
        public Guid Id { get; }
        public DateTime LastUpdated { get; private set; }
        public bool IsActive { get; private set; }
        public int CaptureIntervalMs { get; set; } = 1000; // Default 1 second

        /// <summary>
        /// Creates a new region with the specified bounds
        /// </summary>
        public SelectionRegion(Rectangle bounds)
        {
            Id = Guid.NewGuid();
            Bounds = bounds;
            LastUpdated = DateTime.Now;
            IsActive = true;
        }

        /// <summary>
        /// Updates the region bounds
        /// </summary>
        public void UpdateBounds(Rectangle newBounds)
        {
            Bounds = newBounds;
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// Checks if the region should be updated based on the capture interval
        /// </summary>
        public bool ShouldUpdate()
        {
            return (DateTime.Now - LastUpdated).TotalMilliseconds >= CaptureIntervalMs;
        }

        /// <summary>
        /// Enables or disables the region
        /// </summary>
        public void SetActive(bool active)
        {
            IsActive = active;
        }
    }
}