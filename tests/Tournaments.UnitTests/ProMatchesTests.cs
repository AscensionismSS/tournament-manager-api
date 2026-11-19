using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TournamentCreator.WebApi.Controllers.v1;
using Tournaments.Infrastructure.ExternalServices.PandaScore;
using Xunit;

namespace Tournaments.UnitTests;

public class ProMatchesTests
{
    [Fact]
    public async Task GetOverviewAsync_WhenApiKeyNotConfigured_ShouldReturnNotConfiguredWithoutHttpCalls()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new PandaScoreOptions { ApiKey = "" });

        var service = new PandaScoreProMatchesService(
            httpClient,
            cache,
            options,
            NullLogger<PandaScoreProMatchesService>.Instance);

        var overview = await service.GetOverviewAsync(CancellationToken.None);

        Assert.False(overview.IsConfigured);
        Assert.Empty(overview.LiveMatches);
        Assert.Empty(overview.Tournaments);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetOverviewAsync_ShouldGroupByTournament_FilterPtAndEnStreamsByPlatform_AndCacheResults()
    {
        var now = DateTime.UtcNow;
        var upcomingSoon = now.AddHours(4).ToString("O");
        var upcomingTooFar = now.AddDays(15).ToString("O");

        var runningMatchesJson = $$"""
        [
          {
            "id": 1001,
            "tournament_id": 500,
            "match_type": "best_of",
            "number_of_games": 3,
            "begin_at": "{{now.AddMinutes(-30):O}}",
            "league": { "name": "DreamLeague", "image_url": "https://cdn.example.com/dl.png" },
            "serie": { "full_name": "Season 25" },
            "tournament": { "id": 500, "name": "Playoffs", "tier": "s" },
            "opponents": [
              { "opponent": { "id": 10, "name": "Team Liquid", "acronym": "TL", "image_url": "https://cdn.example.com/tl.png" } },
              { "opponent": { "id": 20, "name": "Team Spirit", "acronym": "TS", "image_url": "https://cdn.example.com/ts.png" } }
            ],
            "results": [
              { "team_id": 10, "score": 1 },
              { "team_id": 20, "score": 0 }
            ],
            "streams_list": [
              { "language": "ru", "main": false, "official": true, "raw_url": "https://www.twitch.tv/ru_cast" },
              { "language": "en", "main": true, "official": true, "raw_url": "https://www.twitch.tv/esl_dota2" },
              { "language": "en", "main": false, "official": true, "raw_url": "https://www.youtube.com/watch?v=abc123" },
              { "language": "pt", "main": false, "official": true, "raw_url": "https://www.twitch.tv/btsbrasiltv" },
              { "language": "pt", "main": false, "official": false, "raw_url": "javascript:alert(1)" }
            ]
          },
          {
            "id": 1002,
            "tournament_id": 800,
            "match_type": "best_of",
            "number_of_games": 3,
            "begin_at": "{{now.AddMinutes(-15):O}}",
            "league": { "name": "EPL Tier 2 Cup" },
            "tournament": { "id": 800, "name": "Group Stage", "tier": "b" },
            "opponents": [],
            "results": [],
            "streams_list": []
          }
        ]
        """;

        var upcomingMatchesJson = $$"""
        [
          {
            "id": 2001,
            "tournament_id": 600,
            "match_type": "best_of",
            "number_of_games": 3,
            "begin_at": "{{upcomingSoon}}",
            "league": { "name": "PGL Wallachia", "image_url": "https://cdn.example.com/pgl.png" },
            "serie": { "full_name": "Season 4" },
            "tournament": { "id": 600, "name": "Group Stage" },
            "opponents": [
              { "opponent": { "id": 30, "name": "Gaimin Gladiators", "acronym": "GG" } },
              { "opponent": { "id": 40, "name": "Tundra Esports", "acronym": "TUN" } }
            ],
            "results": [],
            "streams_list": [
              { "language": "en", "main": true, "official": true, "raw_url": "https://youtu.be/xyz789" }
            ]
          },
          {
            "id": 2002,
            "tournament_id": 700,
            "match_type": "best_of",
            "number_of_games": 5,
            "begin_at": "{{upcomingTooFar}}",
            "league": { "name": "Future Cup" },
            "tournament": { "id": 700, "name": "Finals" },
            "opponents": [],
            "results": [],
            "streams_list": []
          }
        ]
        """;

        var runningTournamentsJson = $$"""
        [
          {
            "id": 500,
            "name": "Playoffs",
            "tier": "s",
            "prizepool": "$1,000,000",
            "begin_at": "{{now.AddDays(-2):O}}",
            "end_at": "{{now.AddDays(3):O}}",
            "league": { "name": "DreamLeague", "image_url": "https://cdn.example.com/dl.png" },
            "serie": { "full_name": "Season 25" }
          },
          {
            "id": 600,
            "name": "Group Stage",
            "tier": "a",
            "prizepool": "$1,000,000",
            "begin_at": "{{now.AddDays(-1):O}}",
            "end_at": "{{now.AddDays(5):O}}",
            "league": { "name": "PGL Wallachia", "image_url": "https://cdn.example.com/pgl.png" },
            "serie": { "full_name": "Season 4" }
          },
          {
            "id": 800,
            "name": "Group Stage",
            "tier": "b",
            "league": { "name": "EPL Tier 2 Cup" }
          },
          {
            "id": 900,
            "name": "Open Qualifier",
            "tier": "c",
            "league": { "name": "Random Tier 3 Cup" }
          }
        ]
        """;

        var handler = new StubHttpHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            string payload = path switch
            {
                "/dota2/matches/running" => runningMatchesJson,
                "/dota2/matches/upcoming" => upcomingMatchesJson,
                "/dota2/tournaments/running" => runningTournamentsJson,
                _ => "[]"
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new PandaScoreOptions
        {
            ApiKey = "test-pandascore-token-123",
            LiveCacheSeconds = 120,
            UpcomingCacheSeconds = 300,
            TournamentsCacheSeconds = 600,
            UpcomingWindowDays = 7
        });

        var service = new PandaScoreProMatchesService(
            httpClient,
            cache,
            options,
            NullLogger<PandaScoreProMatchesService>.Instance);

        var firstOverview = await service.GetOverviewAsync(CancellationToken.None);

        Assert.True(firstOverview.IsConfigured);
        Assert.Single(firstOverview.LiveMatches);
        Assert.Equal(2, firstOverview.Tournaments.Count); // Torneio 700 (> 7 dias) foi filtrado!

        // Torneio 500 (com partida ao vivo e Tier S) vem primeiro, separado do 600; Tier B (800) e C (900) filtrados!
        var dlTournament = firstOverview.Tournaments[0];
        Assert.Equal(500, dlTournament.TournamentId);
        Assert.True(dlTournament.HasLiveMatches);
        Assert.Equal("S", dlTournament.Tier);
        Assert.Equal("MAJOR", dlTournament.TierLabel);
        Assert.Single(dlTournament.LiveMatches);
        Assert.Empty(dlTournament.UpcomingMatches);

        var liveMatch = dlTournament.LiveMatches[0];
        Assert.Equal("Team Liquid", liveMatch.TeamA.Name);
        Assert.Equal("Team Spirit", liveMatch.TeamB.Name);
        Assert.Equal(1, liveMatch.ScoreTeamA);
        Assert.Equal(0, liveMatch.ScoreTeamB);
        Assert.Equal("BO3", liveMatch.Format);
        Assert.Equal("S", liveMatch.Tier);
        Assert.Equal("MAJOR", liveMatch.TierLabel);

        // Streams: apenas PT-BR e EN válidos (ru e javascript: descartados), PT-BR priorizado, Twitch e YouTube identificados
        Assert.Equal(3, liveMatch.Streams.Count);
        Assert.Equal("pt", liveMatch.Streams[0].Language);
        Assert.Equal("PT-BR", liveMatch.Streams[0].LanguageLabel);
        Assert.Equal("Twitch", liveMatch.Streams[0].Platform);
        Assert.Equal("en", liveMatch.Streams[1].Language);
        Assert.Equal("Twitch", liveMatch.Streams[1].Platform);
        Assert.Equal("en", liveMatch.Streams[2].Language);
        Assert.Equal("YouTube", liveMatch.Streams[2].Platform);

        // Segundo torneio separado (PGL Wallachia) com sua partida upcoming e Tier A
        var pglTournament = firstOverview.Tournaments[1];
        Assert.Equal(600, pglTournament.TournamentId);
        Assert.Equal("A", pglTournament.Tier);
        Assert.Equal("PRO TIER A", pglTournament.TierLabel);
        Assert.False(pglTournament.HasLiveMatches);
        Assert.Single(pglTournament.UpcomingMatches);
        Assert.Equal("YouTube", pglTournament.UpcomingMatches[0].Streams[0].Platform);

        Assert.Equal(3, handler.CallCount);

        // Segunda chamada deve usar cache (zero novas chamadas HTTP)
        var secondOverview = await service.GetOverviewAsync(CancellationToken.None);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(2, secondOverview.Tournaments.Count);
    }

    [Fact]
    public void ProMatchesController_ShouldRequireAuthenticationAndNotAllowAnonymous()
    {
        var controllerType = typeof(ProMatchesController);
        var hasAuthorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Length > 0;
        var hasAllowAnonymous = controllerType.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0;

        Assert.True(hasAuthorize);
        Assert.False(hasAllowAnonymous);

        var method = controllerType.GetMethod(nameof(ProMatchesController.GetOverview));
        Assert.NotNull(method);
        var methodAllowAnonymous = method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0;
        Assert.False(methodAllowAnonymous);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }
}
