// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using static Utils.FlagsValues;

    /// <summary>
    /// Specifies issues that can be detected when login credentials are
    /// <see cref="CoherenceCloudLogin.Validate">validated</see>.
    /// </summary>
    [Flags]
    public enum CredentialsIssue
    {
        None = _0,

        UsernameNotProvided = _1,
        UsernameEmpty = _2,
        UsernameInvalidCharacters = _3,
        UsernameTooLong = _4,
        PasswordNotProvided = _5,
        PasswordEmpty = _6,

        OneTimeCodeNotProvided = _7,
        OneTimeCodeEmpty = _8,

        JwtTokenNotProvided = _9,
        JwtTokenEmpty = _10,

        SteamTicketNotProvided = _11,
        SteamTicketEmpty = _12,

        EpicGamesTokenNotProvided = _13,
        EpicGamesTokenEmpty = _14,

        PlayStationTokenNotProvided = _15,
        PlayStationTokenEmpty = _16,

        XboxTokenNotProvided = _17,
        XboxTokenEmpty = _18,

        NintendoTokenEmpty = _19,
        NintendoTokenNotProvided = _20,

        AuthTokenNotProvided = _21,
    }

    /// <summary>
    /// Extension methods for <see cref="CredentialsIssue"/>.
    /// </summary>
    public static class CredentialsIssueExtensions
    {
        /// <summary>
        /// Gets a list of error messages based on the <see cref="CredentialsIssue"/> flags.
        /// </summary>
        /// <param name="issues"> The <see cref="CredentialsIssue"/> to convert into error messages. </param>
        /// <param name="results">
        /// The list to which the error texts will be added.
        /// <remarks>
        /// Any existing items in the list will be cleared.
        /// </remarks>
        /// </param>
        public static void ToErrors(this CredentialsIssue issues, [DisallowNull] List<string> results)
        {
            results.Clear();
            if(issues.HasFlag(CredentialsIssue.None))
            {
                return;
            }

            if(issues.HasFlag(CredentialsIssue.UsernameNotProvided))
            {
                results.Add(ToError(CredentialsIssue.UsernameNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.UsernameEmpty))
            {
                results.Add(ToError(CredentialsIssue.UsernameEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.UsernameInvalidCharacters))
            {
                results.Add(ToError(CredentialsIssue.UsernameInvalidCharacters));
            }

            if(issues.HasFlag(CredentialsIssue.UsernameTooLong))
            {
                results.Add(ToError(CredentialsIssue.UsernameTooLong));
            }

            if(issues.HasFlag(CredentialsIssue.PasswordNotProvided))
            {
                results.Add(ToError(CredentialsIssue.PasswordNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.PasswordEmpty))
            {
                results.Add(ToError(CredentialsIssue.PasswordEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.OneTimeCodeNotProvided))
            {
                results.Add(ToError(CredentialsIssue.OneTimeCodeNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.OneTimeCodeEmpty))
            {
                results.Add(ToError(CredentialsIssue.OneTimeCodeEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.JwtTokenNotProvided))
            {
                results.Add(ToError(CredentialsIssue.JwtTokenNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.JwtTokenEmpty))
            {
                results.Add(ToError(CredentialsIssue.JwtTokenEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.SteamTicketNotProvided))
            {
                results.Add(ToError(CredentialsIssue.SteamTicketNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.SteamTicketEmpty))
            {
                results.Add(ToError(CredentialsIssue.SteamTicketEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.EpicGamesTokenNotProvided))
            {
                results.Add(ToError(CredentialsIssue.EpicGamesTokenNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.EpicGamesTokenEmpty))
            {
                results.Add(ToError(CredentialsIssue.EpicGamesTokenEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.PlayStationTokenNotProvided))
            {
                results.Add(ToError(CredentialsIssue.PlayStationTokenNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.PlayStationTokenEmpty))
            {
                results.Add(ToError(CredentialsIssue.PlayStationTokenEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.XboxTokenNotProvided))
            {
                results.Add(ToError(CredentialsIssue.XboxTokenNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.XboxTokenEmpty))
            {
                results.Add(ToError(CredentialsIssue.XboxTokenEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.NintendoTokenNotProvided))
            {
                results.Add(ToError(CredentialsIssue.NintendoTokenNotProvided));
            }

            if(issues.HasFlag(CredentialsIssue.NintendoTokenEmpty))
            {
                results.Add(ToError(CredentialsIssue.NintendoTokenEmpty));
            }

            if(issues.HasFlag(CredentialsIssue.AuthTokenNotProvided))
            {
                results.Add(ToError(CredentialsIssue.AuthTokenNotProvided));
            }
        }

        /// <summary>
        /// Converts the given <paramref name="issue"/> into a user-facing error message.
        /// </summary>
        /// <param name="issue"> A single <see cref="CredentialsIssue"/> flag to convert into an error message. </param>
        /// <returns>
        /// A string containing the error message corresponding to the <paramref name="issue"/>.
        /// <remarks>
        /// An empty string if the <paramref name="issue"/> is <see cref="CredentialsIssue.None"/>.
        /// </remarks>
        /// </returns>
        [return: NotNull]
        public static string ToError(this CredentialsIssue issue) => issue switch
        {
            CredentialsIssue.UsernameNotProvided => "A username must be provided.",
            CredentialsIssue.UsernameEmpty => "The username can not be empty.",
            CredentialsIssue.UsernameInvalidCharacters => "The username can only contain lower-case letters, numbers, dashes ('-') and underscores ('_').",
            CredentialsIssue.UsernameTooLong => $"The username must be at most {CoherenceCloudLogin.Validator.UsernameMaxLength} characters long.",
            CredentialsIssue.PasswordNotProvided => "A password must be provided.",
            CredentialsIssue.PasswordEmpty => "The password can not be empty.",
            CredentialsIssue.OneTimeCodeNotProvided => "A one-time code must be provided.",
            CredentialsIssue.OneTimeCodeEmpty => "The one-time code can not be empty.",
            CredentialsIssue.JwtTokenNotProvided => "A JWT token must be provided.",
            CredentialsIssue.JwtTokenEmpty => "The JWT token can not be empty.",
            CredentialsIssue.SteamTicketNotProvided => "A Steam ticket must be provided.",
            CredentialsIssue.SteamTicketEmpty => "The Steam ticket can not be empty.",
            CredentialsIssue.EpicGamesTokenNotProvided => "An Epic Games token must be provided.",
            CredentialsIssue.EpicGamesTokenEmpty => "The Epic Games token can not be empty.",
            CredentialsIssue.PlayStationTokenNotProvided => "A PlayStation token must be provided.",
            CredentialsIssue.PlayStationTokenEmpty => "The PlayStation token can not be empty.",
            CredentialsIssue.XboxTokenNotProvided => "An Xbox token must be provided.",
            CredentialsIssue.XboxTokenEmpty => "The Xbox token can not be empty.",
            CredentialsIssue.NintendoTokenNotProvided => "A Nintendo token must be provided.",
            CredentialsIssue.NintendoTokenEmpty => "The Nintendo token can not be empty.",
            CredentialsIssue.AuthTokenNotProvided => "An authentication token must be provided as a command line argument.",
            _ => ""
        };
    }
}
