// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System.Linq;
    using Cloud;
    using Coherence.Toolkit;
    using Portal;
    using Runtime;
    using UnityEditor;
    using UnityEngine;
    using static Coherence.Toolkit.CoherenceCloudLogin;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(CoherenceCloudLogin))]
    internal sealed class CoherenceCloudLoginEditor : BaseEditor
    {
        internal static class GUIContents
        {
            public static readonly GUIContent OnlineDashboardLink = EditorGUIUtility.TrTextContent("Dashboard", ExternalLinks.PortalUrl);

            public static readonly GUIContent CloudUniqueId = EditorGUIUtility.TrTextContent("Cloud Unique Id",
                "(Optional) A locally unique identifier to associate with the guest player account.\n\n" +
                "If left blank, a Cloud Unique Id is generated automatically.\n\n" +
                "Cloud Unique Ids can be used to log into multiple different guest player accounts on the same device. " +
                "This might be useful for local multiplayer games, allowing each player to log into their own guest player account.\n\n" +
                "This value will be ignored for simulators.");
            public static readonly GUIContent DefaultCloudUniqueId = EditorGUIUtility.TrTextContent("Default", "Using default Cloud Unique Id for the guest account.");

            public static readonly GUIContent Username = EditorGUIUtility.TrTextContent("Username", "The username to use to log in to a Player Account.\n\n" +
                                                                                                    "The username must start with a letter or number and can only contain lower case letters, numbers, dashes and underscores.\n\n" +
                                                                                                    "The username must be at most " + CoherenceCloudLogin.Validator.UsernameMaxLength + " characters long.");
            public static readonly GUIContent Password = EditorGUIUtility.TrTextContent("Password", "A non-empty password to use to log in to a Player Account.");
            public static readonly GUIContent AutoSignup = EditorGUIUtility.TrTextContent("Auto-Signup", "Automatically create a new coherence Cloud account with the provided username and password, if one does not exist already?");

            public static readonly GUIContent Ticket = EditorGUIUtility.TrTextContent("Ticket", "Steam ticket for the account.");
            public static readonly GUIContent Identity = EditorGUIUtility.TrTextContent("Identity", "(Optional) The identifier string that will be passed as a parameter to the GetAuthTicketForWebApi method of the Steamworks Web API when the ticket was created.");

            public static readonly GUIContent Token = EditorGUIUtility.TrTextContent("Token");
            public static readonly GUIContent OneTimeCode = EditorGUIUtility.TrTextContent("Code", "One-time code acquired using PlayerAccount.GetOneTimeCode.\n\nOne-time codes expire after a certain time and can only be used once.");

            public static readonly GUIContent PlayerAccountTitle = EditorGUIUtility.TrTextContent("Player Account");
            public static readonly GUIContent PlayerAccountLoggedIn = EditorGUIUtility.TrTextContent("Logged In", "The identity type that was used to log in to the Player Account.");
            public static readonly GUIContent PlayerAccountDisplayName = EditorGUIUtility.TrTextContent("Display Name", "The display name of the Player Account.");
            public static readonly GUIContent PlayerAccountUsername = EditorGUIUtility.TrTextContent("Username", "The display name of the Player Account.");
            public static readonly GUIContent PlayerAccountCloudUniqueId = EditorGUIUtility.TrTextContent("Cloud Unique Id", "A locally unique identifier associated with the guest account.");
            public static readonly GUIContent PlayerAccountGuestId = EditorGUIUtility.TrTextContent("Guest Id", "A globally unique identifier associated with the guest account.\n" +
                                                                                                                "A new Guest Id is generated automatically the first time when logging in as a guest to a particular project, " +
                                                                                                                "and then cached and reused when logging in again as a guest again to the same project on the same device using the same Cloud Unique Id.");

            public static readonly GUIContent NoIdentity = new("None", "Don't provide a identity when logging in with Steam.");
        }

        /// <summary>
        /// Properties to exclude when drawing the default editor at the end of <see cref="OnGUI"/>.
        /// </summary>
        /// <remarks>
        /// This should include all the properties that this editor already handles drawing, so that they are not drawn twice.
        /// </remarks>
        private static readonly string[] PropertiesToExclude =
        {
            "m_Script",
            Property.login,
            Property.logInOnLoad,
            Property.logInSimulators,
            Property.autoSignup,
            Property.username,
            Property.password
        };

        private bool? overrideId;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (target is CoherenceCloudLogin cloudLogin)
            {
                cloudLogin.OnLoggedIn += OnLoggedIn;
                cloudLogin.OnLoginFailed += OnLoginFailed;
            }
        }

        protected override void OnDisable()
        {
            if (target is CoherenceCloudLogin cloudLogin)
            {
                cloudLogin.OnLoggedIn -= OnLoggedIn;
                cloudLogin.OnLoginFailed -= OnLoginFailed;
            }

            base.OnDisable();
        }

        protected override void OnGUI()
        {
            using var loginProperty = serializedObject.FindProperty(Property.login);
            var login = (Login)loginProperty.intValue;

            var cloudLogin = (CoherenceCloudLogin)target;
            var isLoggedIn = cloudLogin.IsLoggedIn;
            var issues = cloudLogin.Validate();

            if (isLoggedIn)
            {
                GUILayout.Label(GUIContents.PlayerAccountTitle, EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                var playerAccount = cloudLogin.PlayerAccount;
                if (playerAccount.LoginInfos.FirstOrDefault() is var loginInfo)
                {
                    EditorGUILayout.LabelField(GUIContents.PlayerAccountLoggedIn, new GUIContent(loginInfo.LoginType switch
                    {
                        _ when cloudLogin.LogsInAsSimulator => "As Simulator",
                        LoginType.Guest or LoginType.LegacyGuest => "As Guest",
                        LoginType.Password => "With Password",
                        LoginType.Steam => "With Steam",
                        LoginType.EpicGames => "With Epic Games",
                        LoginType.PlayStation => "With PlayStation",
                        LoginType.Xbox => "With Xbox",
                        LoginType.Nintendo => "With Nintendo",
                        LoginType.Jwt => "With JWT",
                        LoginType.OneTimeCode => "With One-Time Code",
                        _ => "Unknown"
                    }));
                }

                if (playerAccount.DisplayName is { Length: > 0 } displayName)
                {
                    ContentUtils.DrawCopyableContent(GUIContents.PlayerAccountDisplayName, displayName);
                }

                if (playerAccount.IsGuest)
                {
                    if (playerAccount.CloudUniqueId is { Length: > 0 } cloudUniqueId)
                    {
                        ContentUtils.DrawCopyableContent(GUIContents.PlayerAccountCloudUniqueId, cloudUniqueId);
                    }

                    if (playerAccount.GuestId is { Length: > 0 } guestId)
                    {
                        ContentUtils.DrawCopyableContent(GUIContents.PlayerAccountGuestId, guestId);
                    }
                }
                else if (playerAccount.Username is { Length: > 0 } username)
                {
                    ContentUtils.DrawCopyableContent(GUIContents.PlayerAccountUsername, username);
                }


                EditorGUILayout.EndVertical();
            }
            else
            {
                _ = EditorGUILayout.PropertyField(loginProperty);
                switch (login)
                {
                    case Login.AsGuest:
                        using (var cloudUniqueIdProperty = serializedObject.FindProperty(Property.cloudUniqueId))
                        {
                            overrideId ??= cloudLogin.CloudUniqueId != CloudUniqueId.None;

                            EditorGUILayout.BeginHorizontal();

                            EditorGUILayout.PrefixLabel(GUIContents.CloudUniqueId);

                            var setOverrideId = GUILayout.Toggle(overrideId.Value, GUIContent.none, GUILayout.Width(15f));
                            if (setOverrideId != overrideId.Value)
                            {
                                overrideId = setOverrideId;
                                Undo.RecordObject(cloudLogin, "Set Cloud Unique Id Override");
                                cloudLogin.CloudUniqueId = "";
                                serializedObject.Update();
                            }

                            if (overrideId.Value)
                            {
                                EditorGUILayout.EndHorizontal();
                                _ = EditorGUILayout.PropertyField(cloudUniqueIdProperty, GUIContent.none);
                            }
                            else
                            {
                                EditorGUI.BeginDisabledGroup(disabled: true);
                                GUILayout.Label(GUIContents.DefaultCloudUniqueId);
                                EditorGUI.EndDisabledGroup();
                                EditorGUILayout.EndHorizontal();
                            }
                        }

                        break;
                    case Login.WithPassword:
                        using (var usernameProperty = serializedObject.FindProperty(Property.username))
                        {
                            _ = EditorGUILayout.PropertyField(usernameProperty, GUIContents.Username);
                            if (issues.HasFlag(CredentialsIssue.UsernameNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.UsernameNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.UsernameEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.UsernameEmpty.ToError(), MessageType.Warning);
                            }
                            else
                            {
                                if (issues.HasFlag(CredentialsIssue.UsernameInvalidCharacters))
                                {
                                    EditorGUILayout.HelpBox(CredentialsIssue.UsernameInvalidCharacters.ToError(), MessageType.Warning);
                                }

                                if (issues.HasFlag(CredentialsIssue.UsernameTooLong))
                                {
                                    EditorGUILayout.HelpBox(CredentialsIssue.UsernameTooLong.ToError(), MessageType.Warning);
                                }
                            }

                            using var passwordProperty = serializedObject.FindProperty(Property.password);
                            _ = EditorGUILayout.PropertyField(passwordProperty, GUIContents.Password);
                            if (issues.HasFlag(CredentialsIssue.PasswordNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.PasswordNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.PasswordEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.PasswordEmpty.ToError(), MessageType.Warning);
                            }

                            using var autoSignupProperty = serializedObject.FindProperty(Property.autoSignup);
                            _ = EditorGUILayout.PropertyField(autoSignupProperty, GUIContents.AutoSignup);
                        }

                        break;
                    case Login.WithSteam:
                        using (var ticketProperty = serializedObject.FindProperty(Property.ticket))
                        {
                            _ = EditorGUILayout.PropertyField(ticketProperty, GUIContents.Ticket);
                            if (issues.HasFlag(CredentialsIssue.SteamTicketNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.SteamTicketNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.SteamTicketEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.SteamTicketEmpty.ToError(), MessageType.Warning);
                            }

                            using var identityProperty = serializedObject.FindProperty(Property.identity);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PrefixLabel(GUIContents.Identity);
                            overrideId ??= !string.IsNullOrEmpty(cloudLogin.SteamIdentity);
                            var setOverrideId = GUILayout.Toggle(overrideId.Value, GUIContent.none, GUILayout.Width(15f));
                            if (setOverrideId != overrideId.Value)
                            {
                                overrideId = setOverrideId;
                                Undo.RecordObject(cloudLogin, "Set Steam Identity Override");
                                cloudLogin.SteamIdentity = "";
                                serializedObject.Update();
                            }

                            if (overrideId.Value)
                            {
                                EditorGUILayout.EndHorizontal();
                                _ = EditorGUILayout.PropertyField(identityProperty, GUIContent.none);
                            }
                            else
                            {
                                EditorGUI.BeginDisabledGroup(disabled: true);
                                GUILayout.Label(GUIContents.NoIdentity);
                                EditorGUI.EndDisabledGroup();
                                EditorGUILayout.EndHorizontal();
                            }
                        }

                        break;
                    case Login.WithEpicGames:
                        using (var tokenProperty = serializedObject.FindProperty(Property.token))
                        {
                            _ = EditorGUILayout.PropertyField(tokenProperty, GUIContents.Token);
                            if (issues.HasFlag(CredentialsIssue.EpicGamesTokenNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.EpicGamesTokenNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.EpicGamesTokenEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.EpicGamesTokenEmpty.ToError(), MessageType.Warning);
                            }
                        }

                        break;
                    case Login.WithPlayStation:
                        using (var tokenProperty = serializedObject.FindProperty(Property.token))
                        {
                            _ = EditorGUILayout.PropertyField(tokenProperty, GUIContents.Token);
                            if (issues.HasFlag(CredentialsIssue.PlayStationTokenNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.PlayStationTokenNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.PlayStationTokenEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.PlayStationTokenEmpty.ToError(), MessageType.Warning);
                            }
                        }

                        break;
                    case Login.WithXbox:
                        using (var tokenProperty = serializedObject.FindProperty(Property.token))
                        {
                            _ = EditorGUILayout.PropertyField(tokenProperty, GUIContents.Token);
                            if (issues.HasFlag(CredentialsIssue.XboxTokenNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.XboxTokenNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.XboxTokenEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.XboxTokenEmpty.ToError(), MessageType.Warning);
                            }
                        }

                        break;
                    case Login.WithNintendo:
                        using (var tokenProperty = serializedObject.FindProperty(Property.token))
                        {
                            _ = EditorGUILayout.PropertyField(tokenProperty, GUIContents.Token);
                            if (issues.HasFlag(CredentialsIssue.NintendoTokenNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.NintendoTokenNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.NintendoTokenEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.NintendoTokenEmpty.ToError(), MessageType.Warning);
                            }
                        }

                        break;
                    case Login.WithJwt:
                        using (var tokenProperty = serializedObject.FindProperty(Property.token))
                        {
                            _ = EditorGUILayout.PropertyField(tokenProperty, GUIContents.Token);
                            if (issues.HasFlag(CredentialsIssue.JwtTokenNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.JwtTokenNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.JwtTokenEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.JwtTokenEmpty.ToError(), MessageType.Warning);
                            }
                        }

                        break;
                    case Login.WithOneTimeCode:
                        using (var codeProperty = serializedObject.FindProperty(Property.code))
                        {
                            _ = EditorGUILayout.PropertyField(codeProperty, GUIContents.OneTimeCode);
                            if (issues.HasFlag(CredentialsIssue.OneTimeCodeNotProvided) && Application.isPlaying)
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.OneTimeCodeNotProvided.ToError(), MessageType.Warning);
                            }
                            else if (issues.HasFlag(CredentialsIssue.OneTimeCodeEmpty))
                            {
                                EditorGUILayout.HelpBox(CredentialsIssue.OneTimeCodeEmpty.ToError(), MessageType.Warning);
                            }
                        }

                        break;
                }
            }

            if (cloudLogin.Error is not null)
            {
                EditorGUILayout.HelpBox(cloudLogin.Error.ToString(), MessageType.Error);
                EditorGUILayout.Space(10f);
            }
            else if (string.IsNullOrEmpty(RuntimeSettings.Instance.RuntimeKey))
            {
                GUILayout.Space(10f);
                EditorGUILayout.HelpBox("You must select a Project in the 'Cloud' section of the coherence Hub window to be able to log in.", MessageType.Warning);
            }
            else if (!isLoggedIn)
            {
                DrawOnlineDashboardInfo(login);
            }

            GUILayout.Space(10f);

            using var logInOnLoadProperty = serializedObject.FindProperty(Property.logInOnLoad);
            _ = EditorGUILayout.PropertyField(logInOnLoadProperty);

            GUILayout.BeginHorizontal();
            {
                using var logInSimulatorsProperty = serializedObject.FindProperty(Property.logInSimulators);
                _ = EditorGUILayout.PropertyField(logInSimulatorsProperty);
                ContentUtils.DrawHelpButton(DocumentationKeys.Simulators);
            }
            GUILayout.EndHorizontal();

            if (issues.HasFlag(CredentialsIssue.AuthTokenNotProvided))
            {
                EditorGUILayout.HelpBox(CredentialsIssue.AuthTokenNotProvided.ToError(), MessageType.Warning);
            }

            DrawPropertiesExcluding(serializedObject, PropertiesToExclude);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawOnlineDashboardInfo(Login login)
        {
            #pragma warning disable CS8524
            var configInfo = login switch
            #pragma warning restore CS8524
            {
                Login.AsGuest => "'Guest Auth Enabled' must be ticked in Project Settings on your Online Dashboard for this authentication method to be usable.",
                Login.WithPassword => "'User / Password Auth Enabled' must be ticked in Project Settings on your Online Dashboard for this authentication method to be usable.",
                Login.WithSteam => "'Steam Auth Enabled' must be ticked in Project Settings on your Online Dashboard for this authentication method to be usable.",
                Login.WithEpicGames => "'Epic Auth Enabled' must be ticked in Project Settings on your Online Dashboard for this authentication method to be usable.",
                Login.WithPlayStation => "'PSN Auth Enabled' must be ticked in Project Settings on your Online Dashboard for this authentication method to be usable.",
                Login.WithXbox => "'Xbox Auth Enabled' must be ticked in Project Settings on your Online Dashboard for this authentication method to be usable.",
                Login.WithNintendo => "'Nintendo Auth Enabled' must be ticked in Project Settings on your Online Dashboard for this authentication method to be usable.",
                Login.WithJwt => "'JWT Auth Enabled' must be ticked and a 'JKU Domain' or 'Public Key' provided in Project Settings on your Online Dashboard for this authentication method to be usable.",
                Login.WithOneTimeCode => "'One-Time Code Auth Enabled' must be ticked in Project Settings on your Online Dashboard for this authentication method to be usable.",
            };

            EditorGUILayout.Space(10f);

            EditorGUILayout.HelpBox(configInfo, MessageType.Info);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var url = SharedModuleSections.GetDashboardUrl(PortalLoginDrawer.GetSelectedOrganization()?.slug);
            var content = GUIContents.OnlineDashboardLink;
            content.tooltip = url;
            CoherenceHubLayout.DrawLink(content, url);
            GUILayout.EndHorizontal();
        }

        private void OnLoggedIn(LoginOperation _) => Repaint();
        private void OnLoginFailed(LoginOperationError error) => Repaint();
    }
}
