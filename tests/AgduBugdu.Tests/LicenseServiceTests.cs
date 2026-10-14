using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgduBugdu.App.ViewModels;
using AgduBugdu.Core.Licensing;
using AgduBugdu.Infrastructure.Licensing;
using Xunit;

namespace AgduBugdu.Tests;

public class LicenseServiceTests
{
    private class InMemoryLicenseStorage : ILicenseStorage
    {
        private LicenseRecord? _record;
        private DateTime? _trialStart;

        public LicenseRecord? LoadLicenseRecord() => _record;

        public void SaveLicenseRecord(LicenseRecord record)
        {
            _record = record;
        }

        public void ClearLicenseRecord()
        {
            _record = null;
        }

        public DateTime GetOrCreateTrialStartDate()
        {
            if (!_trialStart.HasValue)
            {
                _trialStart = DateTime.UtcNow;
            }
            return _trialStart.Value;
        }

        public void SetTrialStartDate(DateTime dt)
        {
            _trialStart = dt;
        }

        private long _clockTicks;

        public long GetLastKnownClockTicks() => _clockTicks;

        public void UpdateLastKnownClockTicks(long ticks)
        {
            if (ticks > _clockTicks) _clockTicks = ticks;
        }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ResponseFactory != null)
            {
                return Task.FromResult(ResponseFactory(request));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"status\":\"active\"}", Encoding.UTF8, "application/json")
            });
        }
    }

    [Theory]
    [InlineData("ADM-1234-5678-90AB", LicenseType.Admin)]
    [InlineData("adm-abcd-ef01-2345", LicenseType.Admin)]
    [InlineData("DEV-ABCD-EF01-2345", LicenseType.Dev)]
    [InlineData("dev-9999-8888-7777", LicenseType.Dev)]
    [InlineData("USER-1111-2222-3333", LicenseType.User)]
    [InlineData("user-xxxx-yyyy-zzzz", LicenseType.User)]
    [InlineData("TRIAL-KEY", LicenseType.Trial)]
    [InlineData("", LicenseType.Trial)]
    [InlineData(null, LicenseType.Trial)]
    public void ParseLicenseType_IdentifiesCorrectly(string? key, LicenseType expectedType)
    {
        var result = LicenseService.ParseLicenseType(key!);
        Assert.Equal(expectedType, result);
    }

    [Fact]
    public void InitialState_DefaultsToTrialWith60Days()
    {
        var storage = new InMemoryLicenseStorage();
        var service = new LicenseService(storage);

        Assert.Equal(LicenseType.Trial, service.CurrentLicense.Type);
        Assert.Equal(LicenseStatus.TrialActive, service.CurrentLicense.Status);
        Assert.False(service.CurrentLicense.IsPaid);
        Assert.True(service.CurrentLicense.IsValid);
        Assert.True(service.CurrentLicense.DaysRemaining <= 60 && service.CurrentLicense.DaysRemaining >= 59);
    }

    [Fact]
    public void TrialExpiry_EvaluatesCorrectlyAfter60Days()
    {
        var storage = new InMemoryLicenseStorage();
        storage.SetTrialStartDate(DateTime.UtcNow.AddDays(-65)); // 65 days ago
        var service = new LicenseService(storage);

        Assert.Equal(LicenseType.Trial, service.CurrentLicense.Type);
        Assert.Equal(LicenseStatus.TrialExpired, service.CurrentLicense.Status);
        Assert.Equal(0, service.CurrentLicense.DaysRemaining);
        Assert.False(service.CurrentLicense.IsValid);
    }

    [Fact]
    public void GetMachineHWID_ReturnsStable24CharString()
    {
        var storage = new InMemoryLicenseStorage();
        var service = new LicenseService(storage);

        string hwid1 = service.GetMachineHWID();
        string hwid2 = service.GetMachineHWID();

        Assert.False(string.IsNullOrWhiteSpace(hwid1));
        Assert.Equal(24, hwid1.Length);
        Assert.Equal(hwid1, hwid2);
        Assert.Equal(hwid1.ToUpperInvariant(), hwid1);
    }

    [Fact]
    public async Task ActivateLicense_RejectsMissingCredentials_ForUserAndDev()
    {
        var storage = new InMemoryLicenseStorage();
        var service = new LicenseService(storage);

        var resultUser = await service.ActivateLicenseAsync("USER-1234-5678-90AB", username: null, password: null);
        Assert.False(resultUser.Success);
        Assert.Contains("Username is required", resultUser.Message);

        var resultDev = await service.ActivateLicenseAsync("DEV-1234-5678-90AB", username: "dev_user", password: null);
        Assert.False(resultDev.Success);
        Assert.Contains("Password is required", resultDev.Message);
    }

    [Fact]
    public async Task ActivateLicense_AdminBypassesCredentials()
    {
        var storage = new InMemoryLicenseStorage();
        var handler = new MockHttpMessageHandler
        {
            ResponseFactory = req =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"success\":true,\"status\":\"active\",\"message\":\"machine activated successfully\",\"expires_at\":null}", Encoding.UTF8, "application/json")
                };
            }
        };

        var httpClient = new HttpClient(handler);
        var service = new LicenseService(storage, httpClient);

        var result = await service.ActivateLicenseAsync("ADM-AAAA-BBBB-CCCC", username: null, password: null);

        Assert.True(result.Success);
        Assert.Equal(LicenseType.Admin, service.CurrentLicense.Type);
        Assert.True(service.CurrentLicense.IsPaid);
        Assert.True(service.CurrentLicense.IsValid);
        Assert.Equal("👑", service.CurrentLicense.BadgeIcon);
        Assert.Equal("Admin License", service.CurrentLicense.DisplayTitle);
    }

    [Fact]
    public async Task DeactivateAndRevertToTrial_RestoresTrialMode()
    {
        var storage = new InMemoryLicenseStorage();
        var handler = new MockHttpMessageHandler
        {
            ResponseFactory = req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"status\":\"active\"}", Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new HttpClient(handler);
        var service = new LicenseService(storage, httpClient);

        // Activate admin license
        await service.ActivateLicenseAsync("ADM-1234-5678-9999");
        Assert.True(service.CurrentLicense.IsPaid);

        // Revert to trial
        await service.DeactivateAndRevertToTrialAsync();

        Assert.Equal(LicenseType.Trial, service.CurrentLicense.Type);
        Assert.False(service.CurrentLicense.IsPaid);
        Assert.True(service.CurrentLicense.DaysRemaining > 0);
    }

    [Fact]
    public void LicenseModalViewModel_MaskAndReveal_TogglesCorrectly()
    {
        var storage = new InMemoryLicenseStorage();
        var service = new LicenseService(storage);
        var vm = new LicenseModalViewModel(service);

        Assert.False(vm.IsRevealed);
        Assert.Equal('●', vm.LicensePasswordChar);
        Assert.Equal("Reveal", vm.RevealButtonText);

        vm.ToggleRevealCommand.Execute(null);

        Assert.True(vm.IsRevealed);
        Assert.Equal('\0', vm.LicensePasswordChar);
        Assert.Equal("Hide", vm.RevealButtonText);
    }

    [Fact]
    public void LicenseModalViewModel_KeyPrefixDetection()
    {
        var storage = new InMemoryLicenseStorage();
        var service = new LicenseService(storage);
        var vm = new LicenseModalViewModel(service);

        vm.LicenseKeyInput = "ADM-1111-2222-3333";
        Assert.True(vm.IsAdminKey);
        Assert.False(vm.IsCredentialsRequired);

        vm.LicenseKeyInput = "USER-1111-2222-3333";
        Assert.False(vm.IsAdminKey);
        Assert.True(vm.IsCredentialsRequired);

        vm.LicenseKeyInput = "DEV-1111-2222-3333";
        Assert.False(vm.IsAdminKey);
        Assert.True(vm.IsCredentialsRequired);
    }
}
