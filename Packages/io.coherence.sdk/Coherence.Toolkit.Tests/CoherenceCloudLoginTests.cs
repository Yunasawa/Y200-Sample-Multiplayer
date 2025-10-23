// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System.Threading.Tasks;
    using Cloud;
    using Coherence.Tests;
    using NUnit.Framework;
    using Runtime;
    using Runtime.Tests;

    /// <summary>
    /// Edit mode unit tests for <see cref="CoherenceCloudLogin"/>.
    /// </summary>
    public sealed class CoherenceCloudLoginTests : CoherenceTest
    {
        private string authTokenWas;
        private bool wasSimulator;

        [Test]
        public async Task Retrying_To_Login_When_Encountering_Too_Many_Requests_Exception_Works()
        {
            var cloudLogin = CoherenceCloudLogin.Create(CoherenceCloudLogin.Login.AsGuest);
            using var cloudServiceBuilder = new FakeCloudServiceBuilder()
                .SetShouldMockAuthClient(false)
                .SetupRequestFactory(x => x.SetIsReady().OnSendRequestAsyncCalled(new RequestException(ErrorCode.TooManyRequests)));
            cloudServiceBuilder.AuthClient.ConnectTimeoutAfterSeconds = 3f;
            cloudLogin.Services = cloudServiceBuilder.Build();

            const int expectedLoginAttempts = CoherenceCloudLogin.MaxRetries + 1;
            var loginAttemptsMade = 0;
            cloudServiceBuilder.AuthClient.OnLoggingIn += _ =>
            {
                loginAttemptsMade++;

                // Succeed on the last attempt
                if (loginAttemptsMade is expectedLoginAttempts)
                {
                    cloudServiceBuilder.RequestFactoryBuilder.OnSendRequestAsyncCalled(default(RequestException));
                }
            };
            var loginOperation = cloudLogin.LogInAsync();
            await loginOperation;
            Assert.That(loginAttemptsMade, Is.EqualTo(expectedLoginAttempts));
            Assert.That(loginOperation.HasFailed, Is.False);
            Assert.That(loginOperation.Error, Is.Null);
            Assert.That(loginOperation.Result, Is.EqualTo(cloudLogin.PlayerAccount));
        }

        [Test]
        public async Task Multiple_LoginAsync_Calls_Return_Same_PlayerAccount_With_Owner_Count_Of_One()
        {
            var cloudLogin = CoherenceCloudLogin.Create(CoherenceCloudLogin.Login.AsGuest);
            using var cloudServiceBuilder = new FakeCloudServiceBuilder()
                .SetShouldMockAuthClient(false)
                .SetupRequestFactory(x => x.SetIsReady(false).SetConnectDelay(0.1f));
            cloudServiceBuilder.AuthClient.ConnectTimeoutAfterSeconds = 3f;
            cloudLogin.Services = cloudServiceBuilder.Build();

            var loginOperation1 = cloudLogin.LogInAsync();
            var loginOperation2 = cloudLogin.LogInAsync();

            await cloudServiceBuilder.RequestFactoryBuilder.ConnectAsync();
            await Task.WhenAll(loginOperation1, loginOperation2);

            Assert.That(loginOperation1.HasFailed, Is.False);
            Assert.That(loginOperation2.HasFailed, Is.False);
            Assert.That(loginOperation1.Result, Is.EqualTo(cloudLogin.PlayerAccount));
            Assert.That(loginOperation2.Result, Is.EqualTo(cloudLogin.PlayerAccount));
            Assert.That(cloudLogin.PlayerAccount.OwnerCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Only_Retries_Logging_In_Expected_Number_Of_Times_When_Encountering_Too_Many_Requests_Exception()
        {
            var cloudLogin = CoherenceCloudLogin.Create(CoherenceCloudLogin.Login.AsGuest);
            using var cloudServiceBuilder = new FakeCloudServiceBuilder()
                .SetShouldMockAuthClient(false)
                .SetupRequestFactory(x => x.SetIsReady().OnSendRequestAsyncCalled(new RequestException(ErrorCode.TooManyRequests)));
            cloudServiceBuilder.AuthClient.ConnectTimeoutAfterSeconds = 3f;
            cloudLogin.Services = cloudServiceBuilder.Build();

            var loginAttemptsMade = 0;
            cloudServiceBuilder.AuthClient.OnLoggingIn += _ => loginAttemptsMade++;
            var loginOperation = cloudLogin.LogInAsync();
            await loginOperation;
            Assert.That(loginAttemptsMade, Is.EqualTo(CoherenceCloudLogin.MaxRetries + 1));
            Assert.That(loginOperation.HasFailed, Is.True);
            Assert.That(loginOperation.Error, Is.Not.Null);
            Assert.That(loginOperation.Error.Type, Is.EqualTo(LoginErrorType.TooManyRequests));
        }

        [TestCase(ErrorCode.InvalidConfig)]
        [TestCase(ErrorCode.InvalidCredentials)]
        [TestCase(ErrorCode.CreditLimit)]
        public async Task Logging_In_Fails_After_First_Attempt_When_Encountering_An_Exception_Other_Than_Too_Many_Requests(ErrorCode errorCode)
        {
            var cloudLogin = CoherenceCloudLogin.Create(CoherenceCloudLogin.Login.AsGuest);
            using var cloudServiceBuilder = new FakeCloudServiceBuilder()
                .SetShouldMockAuthClient(false)
                .SetupRequestFactory(x => x.SetIsReady().OnSendRequestAsyncCalled(new RequestException(errorCode)));
            cloudServiceBuilder.AuthClient.ConnectTimeoutAfterSeconds = 3f;
            cloudLogin.Services = cloudServiceBuilder.Build();

            var loginAttemptsMade = 0;
            cloudServiceBuilder.AuthClient.OnLoggingIn += _ => loginAttemptsMade++;
            var loginOperation = cloudLogin.LogInAsync();
            await loginOperation;
            Assert.That(loginAttemptsMade, Is.EqualTo(1));
            Assert.That(loginOperation.HasFailed, Is.True);
            Assert.That(loginOperation.Error, Is.Not.Null);
        }

        [Ignore("Try enabling after #8350 has been merged; it contains changes to FakeCloudServiceBuilder that could enable this test to not fail during OnDestroyAsync.")]
        [TestCase(false), TestCase(true)]
        public async Task Destroying_CoherenceCloudLogin_Cancels_Login_Operation_In_Progress(bool waitForOngoingCloudOperationsToFinishWhenDestroying)
        {
            var cloudLogin = CoherenceCloudLogin.Create(CoherenceCloudLogin.Login.AsGuest);
            using var cloudServiceBuilder = new FakeCloudServiceBuilder()
                .SetShouldMockAuthClient(false)
                .SetupRequestFactory(x => x.SetIsReady(false).SetConnectDelay(1f));
            cloudServiceBuilder.AuthClient.ConnectTimeoutAfterSeconds = 3f;
            cloudLogin.Services = cloudServiceBuilder.Build();

            var loginOperation = cloudLogin.LogInAsync();

            await cloudLogin.OnDestroyAsync(waitForOngoingCloudOperationsToFinishWhenDestroying);

            await loginOperation;

            Assert.That(loginOperation.IsCanceled, Is.True, loginOperation.ToString());
        }

        public override void SetUp()
        {
            base.SetUp();
            authTokenWas = SimulatorUtility.GetArgument(SimulatorUtility.AuthTokenKeyword);
            wasSimulator = SimulatorUtility.IsSimulator;
        }

        public override void TearDown()
        {
            SimulatorUtility.RemoveArgument(SimulatorUtility.AuthTokenKeyword);
            if (authTokenWas is not null)
            {
                SimulatorUtility.SetArgument(SimulatorUtility.AuthTokenKeyword, authTokenWas);
            }

            SimulatorUtility.IsSimulator = wasSimulator;
            base.TearDown();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void CloudUniqueId_Returns_SimulatorInCloudUniqueId_When_UseSharedCloudCredentials_Is_True(bool isSimulator, bool useSharedCloudCredentials)
        {
            SimulatorUtility.IsSimulator = isSimulator;

            SimulatorUtility.RemoveArgument(SimulatorUtility.AuthTokenKeyword);
            if (useSharedCloudCredentials)
            {
                SimulatorUtility.SetArgument(SimulatorUtility.AuthTokenKeyword, SimulatorPlayerAccountProvider.SimulatorInCloudUniqueId);
            }

            var login = CoherenceCloudLogin.Create(CoherenceCloudLogin.Login.AsGuest);

            var actualCloudUniqueId = login.CloudUniqueId;
            if (useSharedCloudCredentials)
            {
                Assert.That(actualCloudUniqueId, Is.EqualTo((string)SimulatorPlayerAccountProvider.SimulatorInCloudUniqueId));
                Assert.That(SimulatorUtility.UseSharedCloudCredentials, Is.True);
            }
            else
            {
                Assert.That(actualCloudUniqueId, Is.Null.Or.Empty);
                Assert.That(SimulatorUtility.UseSharedCloudCredentials, Is.False);
            }
        }
    }
}
