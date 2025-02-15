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
            if (region != null && !_regions.Any(r => r.Id == region.Id))
            {
                _regions.Add(region);
            }
        }

        /// <summary>
        /// Removes a region by its ID
        /// </summary>
        public bool RemoveRegion(Guid id)
        {
            var region = _regions.FirstOrDefault(r => r.Id == id);
            if (region != null)
            {
                _regions.Remove(region);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets all active regions
        /// </summary>
        public IEnumerable<SelectionRegion> GetActiveRegions()
        {
            return _regions.Where(r => r.IsActive);
        }

        /// <summary>
        /// Gets regions that need to be updated based on their interval
        /// </summary>
        public IEnumerable<SelectionRegion> GetRegionsNeedingUpdate()
        {
            return _regions.Where(r => r.IsActive && r.ShouldUpdate());
        }
    }
}