// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using Cloud;
    using Coherence.Tests;
    using NUnit.Framework;

    /// <summary>
    /// Edit Mode unit tests for <see cref="LoginResult"/>.
    /// </summary>
    public class LoginResultTests : CoherenceTest
    {
        private const string ProjectId = "ProjectId";
        private CloudUniqueId CloudUniqueId => new("CloudUniqueId");
        private SessionToken SessionToken => new(MockRequestFactoryBuilder.DefaultSessionToken);

        [Test]
        public void LoginResult_Contains_Username_From_LoginResponse()
        {
            var loginInfo = LoginInfo.WithSessionToken(SessionToken);
            var playerAccount = new PlayerAccount(loginInfo, CloudUniqueId, ProjectId, null);
            const string username = "username";
            var loginResponse = new LoginResponse { Username = username };
            var loginResult = LoginResult.Success(playerAccount, loginResponse);

            Assert.That(loginResult.Username, Is.EqualTo(username));
        }
    }
}
