// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using Coherence.Tests;
    using NUnit.Framework;
    using static CoherenceCloudLogin;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceCloudLogin.Validate"/>.
    /// </summary>
    public sealed class CoherenceCloudLoginValidatorTests : CoherenceTest
    {
        [TestCase(null, CredentialsIssue.UsernameNotProvided)]
        [TestCase("", CredentialsIssue.UsernameEmpty)]
        [TestCase("A", CredentialsIssue.UsernameInvalidCharacters)]
        [TestCase("å", CredentialsIssue.UsernameInvalidCharacters)]
        [TestCase(" ", CredentialsIssue.UsernameInvalidCharacters)]
        [TestCase("abcdefghi-jklmnopqr-stuvwxyzz-123456789_", CredentialsIssue.None)]
        [TestCase("123456789_123456789_123456789_123456789_1", CredentialsIssue.UsernameTooLong)]
        [TestCase("123456789_123456789_123456789_123456789_A", CredentialsIssue.UsernameTooLong | CredentialsIssue.UsernameInvalidCharacters)]
        public void Validate_Username(string username, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(username, "password");
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.PasswordNotProvided)]
        [TestCase("", CredentialsIssue.PasswordEmpty)]
        [TestCase(" ", CredentialsIssue.None)]
        [TestCase("123456789_123456789_123456789_123456789_A!@ä*", CredentialsIssue.None)]
        public void Validate_Password(string password, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create("username", password);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.OneTimeCodeNotProvided)]
        [TestCase("", CredentialsIssue.OneTimeCodeEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_OneTimeCode(string oneTimeCode, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithOneTimeCode, oneTimeCode);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.JwtTokenNotProvided)]
        [TestCase("", CredentialsIssue.JwtTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_JwtToken(string jwtToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithJwt, jwtToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.SteamTicketNotProvided)]
        [TestCase("", CredentialsIssue.SteamTicketEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_SteamTicket(string steamTicket, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithSteam, steamTicket);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.EpicGamesTokenNotProvided)]
        [TestCase("", CredentialsIssue.EpicGamesTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_EpicGamesToken(string epicGamesToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithEpicGames, epicGamesToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.PlayStationTokenNotProvided)]
        [TestCase("", CredentialsIssue.PlayStationTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_PlayStationToken(string playStationToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithPlayStation, playStationToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.XboxTokenNotProvided)]
        [TestCase("", CredentialsIssue.XboxTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_XboxToken(string xboxToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithXbox, xboxToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }

        [TestCase(null, CredentialsIssue.NintendoTokenNotProvided)]
        [TestCase("", CredentialsIssue.NintendoTokenEmpty)]
        [TestCase("1", CredentialsIssue.None)]
        public void Validate_NintendoToken(string nintendoToken, CredentialsIssue expectedIssue)
        {
            var cloudLogin = Create(Login.WithNintendo, nintendoToken);
            var issues = cloudLogin.Validate();
            Assert.That(issues, Is.EqualTo(expectedIssue));
        }
    }
}
