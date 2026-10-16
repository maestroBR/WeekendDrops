using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using WeekendDrops.Models;
using WeekendDrops.Services;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace WeekendDrops.Controllers;

[Injectable]
public class WeekendDropsRouter(JsonUtil jsonUtil, WeekendDropsCallback callback)
    : StaticRouter(jsonUtil,
    [
        new RouteAction<EmptyRequestData>(
            "/weekenddrops/state",
            async (url, info, sessionId, output, cancellationToken) => await callback.GetState(sessionId, url)
        ),
        new RouteAction<EmptyRequestData>(
            "/weekenddrops/dailystate",
            async (url, info, sessionId, output, cancellationToken) => await callback.GetDailyState(sessionId, url)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/claimdaily",
            async (url, info, sessionId, output, cancellationToken) => await callback.ClaimDailyReward(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/claimdailybonus",
            async (url, info, sessionId, output, cancellationToken) => await callback.ClaimDailyBonus(sessionId)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/buyitem",
            async (url, info, sessionId, output, cancellationToken) => await callback.BuyShopItem(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/tradein",
            async (url, info, sessionId, output, cancellationToken) => await callback.RedeemBarter(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/rerollchallenge",
            async (url, info, sessionId, output, cancellationToken) => await callback.RerollChallenge(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/rerolldaily",
            async (url, info, sessionId, output, cancellationToken) => await callback.RerollDaily(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/claimtier",
            async (url, info, sessionId, output, cancellationToken) => await callback.ClaimTier(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/depositgp",
            async (url, info, sessionId, output, cancellationToken) => await callback.DepositGp(sessionId, info.Id)
        ),
        new RouteAction<EmptyRequestData>(
            "/weekenddrops/nightmarket",
            async (url, info, sessionId, output, cancellationToken) => await callback.GetNightMarket(sessionId)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/revealcard",
            async (url, info, sessionId, output, cancellationToken) => await callback.RevealCard(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/buycard",
            async (url, info, sessionId, output, cancellationToken) => await callback.BuyCard(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/deposittarcoin",
            async (url, info, sessionId, output, cancellationToken) => await callback.DepositTarcoin(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/debug",
            async (url, info, sessionId, output, cancellationToken) => await callback.DebugAction(sessionId, info.Id)
        ),
        new RouteAction<EmptyRequestData>(
            "/weekenddrops/records",
            async (url, info, sessionId, output, cancellationToken) => await callback.GetRecords(sessionId)
        ),
        new RouteAction<SeedRecordRequest>(
            "/weekenddrops/seedrecord",
            async (url, info, sessionId, output, cancellationToken) => await callback.SeedRecord(sessionId, info)
        ),
        new RouteAction<RaidResultRequest>(
            "/weekenddrops/raidend",
            async (url, info, sessionId, output, cancellationToken) => await callback.ReportRaidResult(sessionId, info)
        ),
        new RouteAction<EmptyRequestData>(
            "/weekenddrops/contracts",
            async (url, info, sessionId, output, cancellationToken) => await callback.GetContractsState(sessionId)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/acceptcontract",
            async (url, info, sessionId, output, cancellationToken) => await callback.AcceptContract(sessionId, info.Id)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/abandoncontract",
            async (url, info, sessionId, output, cancellationToken) => await callback.AbandonContract(sessionId)
        ),
        new RouteAction<ContractResultRequest>(
            "/weekenddrops/contractresult",
            async (url, info, sessionId, output, cancellationToken) => await callback.ReportContractResult(sessionId, info)
        ),
        new RouteAction<ClientFlagsRequest>(
            "/weekenddrops/clientflags",
            async (url, info, sessionId, output, cancellationToken) => await callback.SetClientFlags(sessionId, info)
        ),
        new RouteAction<EmptyRequestData>(
            "/weekenddrops/friends",
            async (url, info, sessionId, output, cancellationToken) => await callback.GetFriends(sessionId)
        ),
        new RouteAction<EmptyRequestData>(
            "/weekenddrops/squad",
            async (url, info, sessionId, output, cancellationToken) => await callback.GetSquad(sessionId)
        ),
        new RouteAction<EmptyRequestData>(
            "/weekenddrops/collection",
            async (url, info, sessionId, output, cancellationToken) => await callback.GetCollection(sessionId)
        ),
        new RouteAction<StringIdRequest>(
            "/weekenddrops/donate",
            async (url, info, sessionId, output, cancellationToken) => await callback.DonateCollectible(sessionId, info.Id)
        ),
        new RouteAction<GiftRequest>(
            "/weekenddrops/giftgp",
            async (url, info, sessionId, output, cancellationToken) => await callback.SendGift(sessionId, info)
        )
    ])
{ }

[Injectable]
public class WeekendDropsCallback(
    HttpResponseUtil httpResponseUtil,
    WeekendChallengeService challengeService,
    DailyChallengeService dailyService,
    ContractService contractService,
    GpBalanceService gpBalance,
    GpGiftService giftService,
    PersonalBestService records,
    SquadService squadService,
    CollectionService collection,
    TarcoinLoot tarcoin,
    TarcoinBalanceService tarcoinBalance,
    NightMarketService nightMarket)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ValueTask<string> SetClientFlags(MongoId sessionId, ClientFlagsRequest info)
    {
        if (info is { NoScav: true })
        {
            challengeService.SetScavChallengesDisabled();
            dailyService.SetScavChallengesDisabled();
        }

        if (info is { LootNet: true })
        {
            challengeService.SetLootNetActive();
            dailyService.SetLootNetActive();
        }

        var json = JsonSerializer.Serialize(new { result = true }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> GetState(MongoId sessionId, string url)
    {
        var state = challengeService.GetClientState(sessionId);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> GetDailyState(MongoId sessionId, string url)
    {
        var state = dailyService.GetDailyState(sessionId);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> ClaimDailyReward(MongoId sessionId, string challengeId)
    {
        var result = dailyService.ClaimDailyReward(sessionId, challengeId);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> ClaimDailyBonus(MongoId sessionId)
    {
        var result = dailyService.ClaimDailyBonus(sessionId);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> BuyShopItem(MongoId sessionId, string itemId)
    {
        var result = dailyService.BuyShopItem(sessionId, itemId);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> RedeemBarter(MongoId sessionId, string itemId)
    {
        var result = dailyService.RedeemBarter(sessionId, itemId);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> ClaimTier(MongoId sessionId, string tierId)
    {
        bool result = int.TryParse(tierId, out int required)
            && challengeService.ClaimTier(sessionId, required);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> RerollChallenge(MongoId sessionId, string challengeId)
    {
        var result = challengeService.RerollChallenge(sessionId, challengeId ?? "");
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> RerollDaily(MongoId sessionId, string challengeId)
    {
        var result = dailyService.RerollDailyChallenge(sessionId, challengeId ?? "");
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> DepositGp(MongoId sessionId, string countStr)
    {
        bool ok = int.TryParse(countStr, out int count) && count > 0;
        if (ok) gpBalance.Add(sessionId.ToString(), count);
        var json = JsonSerializer.Serialize(new { result = ok, deposited = ok ? count : 0 }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> DepositTarcoin(MongoId sessionId, string countStr)
    {
        var rate = tarcoin.UnitsPerCoin;
        int.TryParse(countStr, out int count);
        bool ok = rate > 0 && count > 0 && count <= int.MaxValue / rate;
        int units = ok ? count * rate : 0;
        if (ok) tarcoinBalance.Add(sessionId.ToString(), units);
        var json = JsonSerializer.Serialize(new { result = ok, deposited = units, rate }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> GetNightMarket(MongoId sessionId)
    {
        var json = JsonSerializer.Serialize(nightMarket.ToDto(sessionId.ToString()), JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> RevealCard(MongoId sessionId, string indexStr)
    {
        if (!int.TryParse(indexStr, out int index)) index = -1;
        var result = nightMarket.Reveal(sessionId.ToString(), index);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> BuyCard(MongoId sessionId, string indexStr)
    {
        if (!int.TryParse(indexStr, out int index)) index = -1;
        var result = nightMarket.Buy(sessionId.ToString(), index);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> GetCollection(MongoId sessionId)
    {
        var state = collection.GetState(sessionId.ToString());
        var json = JsonSerializer.Serialize(state, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> DonateCollectible(MongoId sessionId, string templateId)
    {
        var result = collection.Donate(sessionId.ToString(), templateId);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> GetSquad(MongoId sessionId)
    {
        var state = squadService.GetSquad(sessionId.ToString());
        var json = JsonSerializer.Serialize(state, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> GetFriends(MongoId sessionId)
    {
        var state = new FriendsStateDto { Friends = giftService.ListFriends(sessionId.ToString()) };
        var json = JsonSerializer.Serialize(state, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> SendGift(MongoId sessionId, GiftRequest info)
    {
        var result = giftService.SendGift(sessionId.ToString(), info?.ToId ?? "", info?.Amount ?? 0);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> DebugAction(MongoId sessionId, string action)
    {
        bool result;

        if (action != null && action.StartsWith("daily_", StringComparison.OrdinalIgnoreCase))
        {
            result = dailyService.DebugAction(sessionId, action.Substring("daily_".Length));
        }
        else if (action != null && action.StartsWith("contract_", StringComparison.OrdinalIgnoreCase))
        {
            result = contractService.DebugAction(sessionId, action.Substring("contract_".Length));
        }
        else if (action != null && action.StartsWith("market_", StringComparison.OrdinalIgnoreCase))
        {
            result = nightMarket.DebugAction(sessionId.ToString(), action.Substring("market_".Length));
        }
        else
        {
            result = challengeService.DebugAction(sessionId, action);

            if (result && string.Equals(action, "resetprogress", StringComparison.OrdinalIgnoreCase))
                dailyService.ResetDailyProgress(sessionId);

            if (result && string.Equals(action, "reroll", StringComparison.OrdinalIgnoreCase))
                dailyService.RerollDaily(sessionId);
        }
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> ReportRaidResult(MongoId sessionId, RaidResultRequest info)
    {
        int gpEarned = challengeService.ApplyRaidResult(sessionId, info)
                     + dailyService.ApplyRaidResult(sessionId, info);

        var beaten = records.ApplyRaidResult(sessionId, info);
        gpEarned += beaten.Sum(b => b.GpEarned);

        var json = JsonSerializer.Serialize(new { result = true, gpEarned, records = beaten }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> GetRecords(MongoId sessionId)
    {
        var json = JsonSerializer.Serialize(records.GetState(sessionId), JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> SeedRecord(MongoId sessionId, SeedRecordRequest info)
    {
        records.Seed(sessionId, info.Id, info.Value);
        var json = JsonSerializer.Serialize(new { result = true }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> GetContractsState(MongoId sessionId)
    {
        var state = contractService.GetContractsState(sessionId);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> AcceptContract(MongoId sessionId, string contractId)
    {
        var result = contractService.AcceptContract(sessionId, contractId);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> AbandonContract(MongoId sessionId)
    {
        var result = contractService.AbandonContract(sessionId);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }

    public ValueTask<string> ReportContractResult(MongoId sessionId, ContractResultRequest info)
    {
        var result = contractService.CompleteContract(sessionId, info);
        var json = JsonSerializer.Serialize(new { result }, JsonOptions);
        return new ValueTask<string>(httpResponseUtil.GetBody(json));
    }
}
