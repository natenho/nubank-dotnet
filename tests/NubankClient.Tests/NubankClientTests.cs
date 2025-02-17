using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using Moq;
using NubankClient.Http;
using NubankClient.Model;
using NubankClient.Model.Enums;
using RestSharp;
using Xunit;

namespace NubankClient.Tests
{
    public class NubankClientTests
    {
        private readonly Mock<IHttpClient> _mockRestClient = new Mock<IHttpClient>();
        private readonly List<Event> _events;

        public NubankClientTests()
        {
            _events = new List<Event>
            {
                new Event
                {
                    Title = "Test transaction",
                    Amount = 500,
                    Category = EventCategory.Transaction,
                    Description = "Test transaction description",
                    Time = DateTime.Now.Subtract(TimeSpan.FromDays(1))
                },
                new Event
                {
                    Title = "Test transaction 2",
                    Amount = 50,
                    Category = EventCategory.Transaction,
                    Description = "Test transaction 2 description",
                    Time = DateTime.Now.Subtract(TimeSpan.FromDays(2))
                }
            };
        }

        [Fact]
        public async Task ShouldThrowExceptionWithErrorProvidedWhenLoginFailed()
        {
            MockDiscoveryRequest();

            const string errorMessage = "error message";

            var loginData = new Dictionary<string, object>() {
                { "error", errorMessage }
            };

            _mockRestClient
                .Setup(x => x.PostAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(loginData);

            var nubankClient = new Nubank(_mockRestClient.Object, "login", "password");
            var exception = await Assert.ThrowsAsync<AuthenticationException>(
                async () => await nubankClient.Login()
            );

            Assert.Equal(errorMessage, exception.Message);
        }

        [Fact]
        public async Task ShouldThrowExceptionWithGenericMessageWhenLoginFailedAndErrorIsNotPresent()
        {
            MockDiscoveryRequest();

            const string errorMessage = "Unknow error occurred on trying to do login on Nubank using the entered credentials";

            var loginData = new Dictionary<string, object>() {
            };

            _mockRestClient
                .Setup(x => x.PostAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(loginData);

            var nubankClient = new Nubank(_mockRestClient.Object, "login", "password");
            var exception = await Assert.ThrowsAsync<AuthenticationException>(
                async () => await nubankClient.Login()
            );

            Assert.Equal(errorMessage, exception.Message);
        }

        [Fact]
        public async Task ShouldReturnLoginResponseWithNeedsDeviceAuthorizationWhenEventsNotPresentInLoginResponse()
        {
            MockDiscoveryRequest();

            MockLoginRequestWithouEventsUrl();

            var nubankClient = new Nubank(_mockRestClient.Object, "login", "password");
            var loginResponse = await nubankClient.Login();

            Assert.True(loginResponse.NeedsDeviceAuthorization);
            Assert.NotNull(loginResponse.Code);
        }

        [Fact]
        public async Task ShouldReturnLoginResponseWithNotNeedsDeviceAuthorizationWhenEventsPresentInLoginResponse()
        {
            MockDiscoveryRequest();

            MockLoginRequest();

            var nubankClient = new Nubank(_mockRestClient.Object, "login", "password");
            var loginResponse = await nubankClient.Login();

            Assert.False(loginResponse.NeedsDeviceAuthorization);
            Assert.Null(loginResponse.Code);
        }

        [Fact]
        public async Task ShouldAutenticateWithQrCode()
        {
            MockDiscoveryRequest();
            MockDiscoveryAppRequest();

            MockLoginRequestWithouEventsUrl();
            var nubankClient = new Nubank(_mockRestClient.Object, "login", "password");
            var loginResponse = await nubankClient.Login();

            MockLiftRequest(loginResponse.Code);

            await nubankClient.AutenticateWithQrCode(loginResponse.Code);

            var expectedPayload = new
            {
                qr_code_id = loginResponse.Code,
                type = "login-webapp"
            };

            _mockRestClient.Verify(x => x.PostAsync<Dictionary<string, object>>(
                "lift_url",
                It.Is<object>(o => o.GetHashCode() == expectedPayload.GetHashCode()),
                It.Is<Dictionary<string, string>>(dictionary => IsValidAuthorizationHeader(dictionary))
                ), Times.Once());
        }

        private bool IsValidAuthorizationHeader(Dictionary<string, string> dictionary)
        {
            return dictionary.ContainsKey("Authorization")
                && !string.IsNullOrEmpty(dictionary["Authorization"])
                && !string.IsNullOrEmpty(dictionary["Authorization"].Replace("Bearer ", ""));
        }

        [Fact]
        public async Task ShouldGetTokenIfAutenticateWithQrCodeIsCalledWithoutLoginMade()
        {
            MockDiscoveryRequest();
            MockDiscoveryAppRequest();

            MockLoginRequestWithouEventsUrl();
            var nubankClient = new Nubank(_mockRestClient.Object, "login", "password");

            var code = Guid.NewGuid().ToString();

            MockLiftRequest(code);

            await nubankClient.AutenticateWithQrCode(code);

            var expectedPayload = new
            {
                qr_code_id = code,
                type = "login-webapp"
            };

            _mockRestClient.Verify(x => x.PostAsync<Dictionary<string, object>>(
                    "lift_url",
                    It.Is<object>(o => o.GetHashCode() == expectedPayload.GetHashCode()),
                    It.Is<Dictionary<string, string>>(dictionary => IsValidAuthorizationHeader(dictionary))
                ), Times.Once());
        }

        [Fact]
        public async Task ShouldThrowExceptionWhenLoginWasNotCalled()
        {
            MockDiscoveryRequest();

            MockLoginRequest();

            var getEventsResponse = new GetEventsResponse
            {
                Events = _events
            };

            _mockRestClient
                .Setup(x => x.GetAsync<GetEventsResponse>(
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>()
                ))
                .ReturnsAsync(getEventsResponse);

            var nubankClient = new Nubank(_mockRestClient.Object, "login", "password");
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => nubankClient.GetEvents());
        }

        [Fact]
        public async Task ShouldGetEvents()
        {
            MockDiscoveryRequest();

            MockLoginRequest();

            var getEventsResponse = new GetEventsResponse
            {
                Events = _events
            };

            _mockRestClient
                .Setup(x => x.GetAsync<GetEventsResponse>(
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>()
                ))
                .ReturnsAsync(getEventsResponse);

            var nubankClient = new Nubank(_mockRestClient.Object, "login", "password");
            await nubankClient.Login();
            var actualEvents = await nubankClient.GetEvents();

            Assert.Equal(_events, actualEvents);
            _mockRestClient.Verify(x => x.GetAsync<GetEventsResponse>(
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>()
                ), Times.Once());
        }

        private void MockDiscoveryRequest()
        {
            var discoveryData = new Dictionary<string, string>() {
                { "login", "login_url" }
            };

            _mockRestClient
                .Setup(x => x.GetAsync<Dictionary<string, string>>("https://prod-s0-webapp-proxy.nubank.com.br/api/discovery"))
                .ReturnsAsync(discoveryData);
        }

        private void MockLoginRequest()
        {
            var eventsLink = new Dictionary<string, object>() { { "href", "events_url" } };
            var resetPasswordUrl = new Dictionary<string, object>() { { "href", "reset_password_url" } };
            var links = new Dictionary<string, object>() {
                { "events", eventsLink },
                { "reset_password", resetPasswordUrl }
            };

            var responseLogin = new Dictionary<string, object>() {
                { "access_token", "eyJhbGciOiJSUzI1Ni" },
                { "token_type", "bearer" },
                { "_links", links },
            };

            _mockRestClient
                .Setup(x => x.PostAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(responseLogin);
        }

        private void MockLoginRequestWithouEventsUrl()
        {
            var resetPasswordUrl = new Dictionary<string, object>() { { "href", "reset_password_url" } };
            var links = new Dictionary<string, object>() {
                { "reset_password", resetPasswordUrl }
            };

            var responseLogin = new Dictionary<string, object>() {
                { "access_token", "eyJhbGciOiJSUzI1Ni" },
                { "token_type", "bearer" },
                { "_links", links },
            };

            _mockRestClient
                .Setup(x => x.PostAsync<Dictionary<string, object>>("login_url", It.IsAny<object>()))
                .ReturnsAsync(responseLogin);
        }

        private void MockLiftRequest(string code)
        {
            var eventsLink = new Dictionary<string, object>() { { "href", "events_url" } };
            var resetPasswordUrl = new Dictionary<string, object>() { { "href", "reset_password_url" } };
            var links = new Dictionary<string, object>() {
                { "events", eventsLink },
                { "reset_password", resetPasswordUrl }
            };

            var responseLift = new Dictionary<string, object>() {
                { "access_token", "eyJhbGciOiJSUzI1Ni" },
                { "token_type", "bearer" },
                { "_links", links },
            };

            _mockRestClient
                .Setup(x => x.PostAsync<Dictionary<string, object>>(
                    "lift_url",
                    It.IsAny<object>(),
                    It.IsAny<Dictionary<string, string>>())
                 )
                .ReturnsAsync(responseLift);
        }

        private void MockDiscoveryAppRequest()
        {
            var discoveryData = new Dictionary<string, string>() {
                { "lift", "lift_url" }
            };

            _mockRestClient
                .Setup(x => x.GetAsync<Dictionary<string, string>>("https://prod-s0-webapp-proxy.nubank.com.br/api/app/discovery"))
                .ReturnsAsync(discoveryData);
        }
    }
}