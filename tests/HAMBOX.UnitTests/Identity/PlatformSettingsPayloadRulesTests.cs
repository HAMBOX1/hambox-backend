using HAMBOX.Modules.Identity.Application.Features.PlatformSettings;
using Xunit;

namespace HAMBOX.UnitTests.Identity;

public sealed class PlatformSettingsPayloadRulesTests
{
    [Theory]
    [InlineData("queue", """{"maxConcurrency":0}""")]
    [InlineData("queue", """{"batchSize":-1}""")]
    [InlineData("authentication", """{"minimumPasswordLength":-5}""")]
    [InlineData("authentication", """{"sessionTimeoutMinutes":0}""")]
    [InlineData("inventory", """{"reservationTimeoutMinutes":0}""")]
    [InlineData("commerce", """{"taxRatePercent":150}""")]
    [InlineData("otp", """{"codeLength":2}""")]
    [InlineData("email", """{"smtpPort":70000}""")]
    [InlineData("general", """{"contactEmail":"not-an-email"}""")]
    [InlineData("queue", """{"batchSize":"many"}""")]
    [InlineData("purchasePoints", """{"pointsPerUsd":-1}""")]
    [InlineData("purchasePoints", """{"pointsPerUsd":99999}""")]
    [InlineData("purchasePoints", """{"amountBasis":"Gross"}""")]
    [InlineData("queue", "[1,2]")]
    [InlineData("queue", "{broken")]
    public void FindProblem_RejectsOutOfRangeOrMalformedValues(string category, string json) =>
        Assert.NotNull(PlatformSettingsPayloadRules.FindProblem(category, json));

    [Theory]
    [InlineData("queue", """{"workerIntervalSeconds":30,"batchSize":25,"maxConcurrency":4,"visibilityTimeoutSeconds":300}""")]
    [InlineData("authentication", """{"minimumPasswordLength":8,"sessionTimeoutMinutes":15,"adminOtpEnabled":true}""")]
    [InlineData("general", """{"contactEmail":"help@example.com","supportEmail":""}""")]
    [InlineData("queue", """{"BatchSize":10}""")]
    [InlineData("queue", "{}")]
    [InlineData("purchasePoints", """{"enabled":true,"pointsPerUsd":2.5,"amountBasis":"Total"}""")]
    [InlineData("purchasePoints", """{"pointsPerUsd":0,"amountBasis":"NetOfTax"}""")]
    [InlineData("maintenance", """{"enabled":false}""")]
    public void FindProblem_AcceptsValidAndPartialPayloads(string category, string json) =>
        Assert.Null(PlatformSettingsPayloadRules.FindProblem(category, json));
}
