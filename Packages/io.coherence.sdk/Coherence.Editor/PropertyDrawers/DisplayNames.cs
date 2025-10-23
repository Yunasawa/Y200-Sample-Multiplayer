// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Utility class for converting <see cref="Type"/> or other <see cref="MemberInfo"/>
    /// into user-facing <see cref="GUIContent"/>.
    /// </summary>
    /// <remarks>
    /// Supports <see cref="DisplayNameAttribute"/>, <see cref="InspectorNameAttribute"/>
    /// and <see cref="TooltipAttribute"/> for customizing the displayed text and tooltip.
    /// </remarks>
    internal static class DisplayNames
    {
        private static readonly Dictionary<MemberInfo, GUIContent> Cache = new();

        public static GUIContent Get(MemberInfo member, string removeSuffix = null)
        {
            if (Cache.TryGetValue(member, out var result))
            {
                return result;
            }

            if (member.GetCustomAttribute<DisplayNameAttribute>() is { } displayNameAttribute)
            {
                result = new(displayNameAttribute.Name, displayNameAttribute.Tooltip);
                Cache.Add(member, result);
                return result;
            }

            string displayName;
            if (member.GetCustomAttribute<InspectorNameAttribute>() is { } inspectorNameAttribute)
            {
                displayName = inspectorNameAttribute.displayName;
            }
            else
            {
                displayName = member.Name;
                if (removeSuffix is { Length: > 0 } && displayName.EndsWith(removeSuffix))
                {
                    displayName = displayName[..^removeSuffix.Length];
                }

                displayName = ObjectNames.NicifyVariableName(displayName);
            }

            var tooltip = member.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";
            result = new(displayName, tooltip);
            Cache.Add(member, result);
            return result;
        }
    }
}
