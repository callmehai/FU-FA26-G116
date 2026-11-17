using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Infrastructure.Ai;
using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>
/// SummaryService picks the hosted harness by <see cref="ILanguageModel.Remote"/>, which defaults to false on the port:
/// the real API client must say true and the in-house one false, or ordinary meetings would quietly lose the API harness.
/// </summary>
public sealed class LanguageModelRemoteTests
{
    private sealed class NoClients : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No call is made here.");
    }

    [Fact]
    public void The_api_an_administrator_chose_is_remote_and_the_in_house_model_is_not()
    {
        ILanguageModel api = new ApiLanguageModelFactory(new NoClients())
            .Create("https://api.example.vn/v1", "deepseek-v4", "sk-test-1234567890", 1, 1024, "none");
        ILanguageModel local = new LanguageModelClient(new NoClients(), Options.Create(new LanguageModelOptions()),
            TimeProvider.System, NullLogger<LanguageModelClient>.Instance);

        Assert.True(api.Remote);
        Assert.False(local.Remote);
    }
}
