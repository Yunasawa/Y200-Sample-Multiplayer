// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System.Text.RegularExpressions;

    public partial class CoherenceCloudLogin
    {
        /// <summary>
        /// Responsible for <see cref="Validate">validating</see> the login credentials
        /// provided in a <see cref="CoherenceCloudLogin"/> instance.
        /// </summary>
        internal sealed class Validator
        {
            /// <summary>
            /// The maximum number of characters allowed in a username.
            /// </summary>
            internal const int UsernameMaxLength = 40;

            private const string UsernameValidCharactersRegexPattern = "^[0-9a-z][0-9a-z-_]*$";
            private readonly Regex usernameValidCharactersRegex = new(UsernameValidCharactersRegexPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

            public CredentialsIssue Validate(CoherenceCloudLogin cloudLogin) => cloudLogin.login switch
            {
                _ when cloudLogin.IsSimulator => cloudLogin.LogInSimulators && string.IsNullOrEmpty(SimulatorUtility.AuthToken) ? CredentialsIssue.AuthTokenNotProvided : CredentialsIssue.None,
                Login.WithPassword => ValidateUsernameAndPassword(cloudLogin),
                Login.AsGuest => CredentialsIssue.None,
                Login.WithSteam => cloudLogin.SteamTicket is not { } steamTicket ? CredentialsIssue.SteamTicketNotProvided
                    : steamTicket.Length is 0 ? CredentialsIssue.SteamTicketEmpty
                    : CredentialsIssue.None,
                Login.WithEpicGames => cloudLogin.EpicGamesToken is not { } epicGamesToken ? CredentialsIssue.EpicGamesTokenNotProvided
                    : epicGamesToken.Length is 0 ? CredentialsIssue.EpicGamesTokenEmpty
                    : CredentialsIssue.None,
                Login.WithPlayStation => cloudLogin.PlayStationToken is not { } playStationToken ? CredentialsIssue.PlayStationTokenNotProvided
                    : playStationToken.Length is 0 ? CredentialsIssue.PlayStationTokenEmpty
                    : CredentialsIssue.None,
                Login.WithXbox => cloudLogin.XboxToken is not { } xboxToken ? CredentialsIssue.XboxTokenNotProvided
                    : xboxToken.Length is 0 ? CredentialsIssue.XboxTokenEmpty
                    : CredentialsIssue.None,
                Login.WithNintendo => cloudLogin.NintendoToken is not { } nintendoToken ? CredentialsIssue.NintendoTokenNotProvided
                    : nintendoToken.Length is 0 ? CredentialsIssue.NintendoTokenEmpty
                    : CredentialsIssue.None,
                Login.WithJwt => cloudLogin.JwtToken is not { } jwtToken ? CredentialsIssue.JwtTokenNotProvided
                    : jwtToken.Length is 0 ? CredentialsIssue.JwtTokenEmpty
                    : CredentialsIssue.None,
                Login.WithOneTimeCode => cloudLogin.OneTimeCode is not { } oneTimeCode ? CredentialsIssue.OneTimeCodeNotProvided
                    : oneTimeCode.Length is 0 ? CredentialsIssue.OneTimeCodeEmpty
                    : CredentialsIssue.None,
                _ => CredentialsIssue.None
            };

            private CredentialsIssue ValidateUsernameAndPassword(CoherenceCloudLogin cloudLogin)
            {
                CredentialsIssue issues;
                if (cloudLogin.Username is not { } username)
                {
                    issues = CredentialsIssue.UsernameNotProvided;
                }
                else if (username.Length is 0)
                {
                    issues = CredentialsIssue.UsernameEmpty;
                }
                else
                {
                    issues = !usernameValidCharactersRegex.IsMatch(username) ? CredentialsIssue.UsernameInvalidCharacters : CredentialsIssue.None;

                    if (cloudLogin.Username.Length > UsernameMaxLength)
                    {
                        issues |= CredentialsIssue.UsernameTooLong;
                    }
                }

                if (cloudLogin.Password is not { } password)
                {
                    issues |= CredentialsIssue.PasswordNotProvided;
                }
                else if (password.Length is 0)
                {
                    issues |= CredentialsIssue.PasswordEmpty;
                }

                return issues;
            }
        }
    }
}
