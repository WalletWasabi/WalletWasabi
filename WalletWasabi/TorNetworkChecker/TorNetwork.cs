using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WalletWasabi.TorNetworkChecker;

public class TorNetwork : ITorNetwork
{
    private const string IssuesPath = "https://gitlab.torproject.org/api/v4/projects/786/repository/tree?path=content/issues";
    private static readonly Uri IssuesRoot = new("https://gitlab.torproject.org/tpo/tpa/status-site/-/raw/main/");

    private readonly IDeserializer _deserializer;
    private readonly IHttpClientFactory _httpClientFactory;

    public TorNetwork(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;

        Issues = GetIssueFilenames()
            .SelectMany(uris => uris.ToObservable()
                .SelectMany(GetIssueFromUri));

        _deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public IObservable<Issue> Issues { get; }

    private static IObservable<Uri> ParseResponse(string responseText)
    {
        return JArray.Parse(responseText)
            .Select(d => d["path"])
            .Select(x => x.ToString())
            .Where(x => x.EndsWith(".md"))
            .Select(filename => new Uri(IssuesRoot, filename))
            .ToObservable();
    }

    private IObservable<IList<Uri>> GetIssueFilenames()
    {
        var input = Observable
            .Using(() => _httpClientFactory.CreateClient(),
                httpClient => Observable.FromAsync(() => httpClient.GetStringAsync(IssuesPath)))
            .SelectMany(s => ParseResponse(s).ToList());

        return input;
    }

    private IObservable<Issue> GetIssueFromUri(Uri path)
    {
        var observable = Observable
            .Using(() => _httpClientFactory.CreateClient(),
                httpClient => Observable.FromAsync(() => httpClient.GetStringAsync(path)))
            .Select(GetIssueFromContent);
        return observable;
    }

    private Issue GetIssueFromContent(string content)
    {
        var regex = @"---\s+(.*)\s+---(.*)";
        var matches = Regex.Match(content, regex, RegexOptions.Singleline);
        var yml = matches.Groups[1].Value;

        // Still unused
        var description = matches.Groups[2].Value;

        var issue = _deserializer.Deserialize<Issue>(yml);
        return issue;
    }
}