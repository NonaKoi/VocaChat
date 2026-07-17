using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VocaChat.Data;
using VocaChat.Models;

namespace VocaChat.Services;

/// <summary>
/// 负责 AI 账号之间有方向关系的默认值、查询、保存和互动记录。
/// </summary>
public sealed class AiRelationshipService
{
    private readonly VocaChatDbContextFactory _dbContextFactory;

    public AiRelationshipService(VocaChatDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory
            ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// 返回一个账号对其他所有已有账号的关系；未保存的组合使用默认值。
    /// </summary>
    public bool TryGetRelationshipsFrom(
        Guid fromAiAccountId,
        out IReadOnlyList<AiRelationship> relationships)
    {
        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();

        if (!dbContext.AiAccounts.Any(account => account.Id == fromAiAccountId))
        {
            relationships = Array.Empty<AiRelationship>();
            return false;
        }

        Dictionary<Guid, AiRelationship> storedRelationships =
            dbContext.AiRelationships
                .AsNoTracking()
                .Where(relationship =>
                    relationship.FromAiAccountId == fromAiAccountId)
                .ToDictionary(relationship => relationship.ToAiAccountId);

        relationships = dbContext.AiAccounts
            .AsNoTracking()
            .Where(account => account.Id != fromAiAccountId)
            .OrderBy(account => account.CreatedAt)
            .ThenBy(account => account.Id)
            .Select(account => account.Id)
            .AsEnumerable()
            .Select(toAiAccountId =>
                storedRelationships.GetValueOrDefault(toAiAccountId)
                ?? new AiRelationship(fromAiAccountId, toAiAccountId))
            .ToList()
            .AsReadOnly();
        return true;
    }

    /// <summary>
    /// 返回两个账号之间指定方向的关系；未保存时返回默认值。
    /// </summary>
    public AiRelationshipOperationStatus TryGetRelationship(
        Guid fromAiAccountId,
        Guid toAiAccountId,
        out AiRelationship? relationship)
    {
        relationship = null;
        AiRelationshipOperationStatus validationStatus = ValidateAccountPair(
            fromAiAccountId,
            toAiAccountId);

        if (validationStatus != AiRelationshipOperationStatus.Success)
        {
            return validationStatus;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        relationship = dbContext.AiRelationships
            .AsNoTracking()
            .SingleOrDefault(item =>
                item.FromAiAccountId == fromAiAccountId
                && item.ToAiAccountId == toAiAccountId)
            ?? new AiRelationship(fromAiAccountId, toAiAccountId);
        return AiRelationshipOperationStatus.Success;
    }

    /// <summary>
    /// 验证并保存指定方向的熟悉度、好感度和信任度。
    /// </summary>
    public AiRelationshipOperationStatus TryUpdateRelationship(
        Guid fromAiAccountId,
        Guid toAiAccountId,
        int familiarity,
        int affinity,
        int trust,
        out AiRelationship? relationship)
    {
        relationship = null;

        if (!IsPercentage(familiarity)
            || affinity is < -100 or > 100
            || !IsPercentage(trust))
        {
            return AiRelationshipOperationStatus.ValueOutOfRange;
        }

        AiRelationshipOperationStatus validationStatus = ValidateAccountPair(
            fromAiAccountId,
            toAiAccountId);

        if (validationStatus != AiRelationshipOperationStatus.Success)
        {
            return validationStatus;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiRelationship storedRelationship = GetOrCreateTrackedRelationship(
            dbContext,
            fromAiAccountId,
            toAiAccountId);
        storedRelationship.Update(familiarity, affinity, trust);
        dbContext.SaveChanges();
        relationship = storedRelationship;
        return AiRelationshipOperationStatus.Success;
    }

    /// <summary>
    /// 为两个方向各记录一次同一时刻的实际互动。
    /// </summary>
    public AiRelationshipOperationStatus TryRecordInteraction(
        Guid firstAiAccountId,
        Guid secondAiAccountId,
        DateTime occurredAt)
    {
        AiRelationshipOperationStatus validationStatus = ValidateAccountPair(
            firstAiAccountId,
            secondAiAccountId);

        if (validationStatus != AiRelationshipOperationStatus.Success)
        {
            return validationStatus;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        AiRelationship firstToSecond = GetOrCreateTrackedRelationship(
            dbContext,
            firstAiAccountId,
            secondAiAccountId);
        AiRelationship secondToFirst = GetOrCreateTrackedRelationship(
            dbContext,
            secondAiAccountId,
            firstAiAccountId);

        firstToSecond.RecordInteraction(occurredAt);
        secondToFirst.RecordInteraction(occurredAt);
        dbContext.SaveChanges();
        return AiRelationshipOperationStatus.Success;
    }

    private AiRelationshipOperationStatus ValidateAccountPair(
        Guid fromAiAccountId,
        Guid toAiAccountId)
    {
        if (fromAiAccountId == toAiAccountId)
        {
            return AiRelationshipOperationStatus.SelfRelationshipNotAllowed;
        }

        using VocaChatDbContext dbContext = _dbContextFactory.CreateDbContext();
        int existingAccountCount = dbContext.AiAccounts.Count(account =>
            account.Id == fromAiAccountId || account.Id == toAiAccountId);

        return existingAccountCount == 2
            ? AiRelationshipOperationStatus.Success
            : AiRelationshipOperationStatus.AccountNotFound;
    }

    private static AiRelationship GetOrCreateTrackedRelationship(
        VocaChatDbContext dbContext,
        Guid fromAiAccountId,
        Guid toAiAccountId)
    {
        AiRelationship? relationship = dbContext.AiRelationships
            .SingleOrDefault(item =>
                item.FromAiAccountId == fromAiAccountId
                && item.ToAiAccountId == toAiAccountId);

        if (relationship is not null)
        {
            return relationship;
        }

        relationship = new AiRelationship(fromAiAccountId, toAiAccountId);
        dbContext.AiRelationships.Add(relationship);
        return relationship;
    }

    private static bool IsPercentage(int value)
    {
        return value is >= 0 and <= 100;
    }
}
