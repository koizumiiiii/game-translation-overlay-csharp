using System;
using System.Collections.Generic;
using System.Linq;

namespace GameTranslationOverlay.Core.Region
{
    /// <summary>
    /// Manages multiple selection regions for OCR processing
    /// </summary>
    public class RegionManager
    {
        private readonly List<SelectionRegion> _regions = new List<SelectionRegion>();

        /// <summary>
        /// Adds a new region to manage
        /// </summary>
        public void AddRegion(SelectionRegion region)
        {
            // TODO: Add region to list if not null and not already exists
        }

        /// <summary>
        /// Removes a region by its ID
        /// </summary>
        public bool RemoveRegion(Guid id)
        {
            // TODO: Remove region with matching ID
            // Return true if found and removed, false if not found
        }

        /// <summary>
        /// Gets all active regions
        /// </summary>
        public IEnumerable<SelectionRegion> GetActiveRegions()
        {
            // TODO: Return all regions where IsActive is true
        }

        /// <summary>
        /// Gets regions that need to be updated based on their interval
        /// </summary>
        public IEnumerable<SelectionRegion> GetRegionsNeedingUpdate()
        {
            // TODO: Return active regions where ShouldUpdate() returns true
        }
    }
}