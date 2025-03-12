// GameTranslationOverlay/Core/Licensing/LicenseManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using GameTranslationOverlay.Core.Configuration;

namespace GameTranslationOverlay.Core.Licensing
{
    /// <summary>
    /// ライセンスの種類
    /// </summary>
    public enum LicenseType
    {
        Free = 0,
        Basic = 1,
        Pro = 2
    }

    /// <summary>
    /// プレミアム機能の種類
    /// </summary>
    public enum PremiumFeature
    {
        